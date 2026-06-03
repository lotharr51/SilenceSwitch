#Requires -Version 5.1
<#
.SYNOPSIS
    Run Windows App Certification Kit (WACK) against a built SilenceSwitch MSIX.

.DESCRIPTION
    1. Installs the MSIX (requires Developer Mode or a trusted signing cert).
    2. Resets WACK state and runs a "windowsapp" certification pass.
    3. Uninstalls the package (even on failure).
    4. Exits 0 on pass, 1 on any failure — suitable for CI pipeline gating.

.PARAMETER MsixPath
    Full path to the built .msix file.

.PARAMETER ReportPath
    Optional. Path for the XML report. Defaults to <MsixPath>.wack-report.xml.

.EXAMPLE
    .\validate-wack.ps1 -MsixPath .\publish\SilenceSwitch_1.0.0.0_x64.msix

.NOTES
    Prerequisites:
      - Windows App Certification Kit (install via "Windows SDK" setup,
        select "Windows App Certification Kit" workload).
      - Developer Mode enabled  OR  MSIX signed with a trusted certificate.
      - Run as Administrator (required by appcert.exe).
#>
param(
    [Parameter(Mandatory)]
    [string]$MsixPath,

    [string]$ReportPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Locate appcert.exe
# ---------------------------------------------------------------------------
$appcertDir = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit"
$appcert    = Join-Path $appcertDir "appcert.exe"

if (-not (Test-Path $appcert)) {
    Write-Error @"
appcert.exe not found at:
  $appcert

Install the Windows App Certification Kit:
  1. Download the Windows SDK from https://developer.microsoft.com/windows/downloads/windows-sdk/
  2. Run the installer and select 'Windows App Certification Kit'.
"@
    exit 1
}

if (-not (Test-Path $MsixPath)) {
    Write-Error "MSIX not found: '$MsixPath'"
    exit 1
}

if (-not $ReportPath) {
    $ReportPath = [IO.Path]::ChangeExtension($MsixPath, ".wack-report.xml")
}

# ---------------------------------------------------------------------------
# Install the package
# ---------------------------------------------------------------------------
Write-Host "Installing '$MsixPath' ..."
Add-AppxPackage -Path $MsixPath -ErrorAction Stop

$pkg = Get-AppxPackage -Name "SilenceSwitch" -ErrorAction SilentlyContinue |
       Select-Object -First 1

if (-not $pkg) {
    Write-Error "Package 'SilenceSwitch' not found after installation."
    exit 1
}
Write-Host "Installed: $($pkg.PackageFullName)"

# ---------------------------------------------------------------------------
# Run WACK (always uninstall in the finally block)
# ---------------------------------------------------------------------------
$passed = $false
try {
    Write-Host "Resetting WACK state..."
    & $appcert reset
    if ($LASTEXITCODE -ne 0) { throw "appcert reset failed (exit $LASTEXITCODE)" }

    Write-Host "Running WACK certification..."
    & $appcert test `
        -packagefullname $pkg.PackageFullName `
        -apptype         windowsapp           `
        -reportoutputpath $ReportPath

    if ($LASTEXITCODE -eq 0) {
        $passed = $true
        Write-Host ""
        Write-Host "WACK PASSED" -ForegroundColor Green
        Write-Host "Report: $ReportPath"
    }
    else {
        Write-Host ""
        Write-Host "WACK FAILED (appcert exit code $LASTEXITCODE)" -ForegroundColor Red
        Write-Host "Report: $ReportPath"
    }
}
finally {
    Write-Host "Removing $($pkg.PackageFullName) ..."
    Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
}

exit ($passed ? 0 : 1)
