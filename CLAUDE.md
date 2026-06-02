# SilenceSwitch

## Project

Windows 11 system tray app that auto-switches the default audio output back to headphones after a silence timeout.

## Stack

- .NET 8.0, C#, Windows Forms (tray icon + settings dialog)
- NAudio 2.2.1 (audio device enumeration, peak metering)
- IPolicyConfig COM interop (setting default audio endpoint)

## Structure

```
src/SilenceSwitch/
  Program.cs                       Entry point, single-instance, first-run flow
  Config/AppSettings.cs            JSON settings in %APPDATA%\SilenceSwitch\
  Audio/PolicyConfigClient.cs      COM interop — DO NOT reorder interface methods (vtable)
  Audio/AudioDeviceManager.cs      NAudio wrapper for device ops
  Audio/SilenceMonitor.cs          Timer + silence detection logic
  App/TrayApplicationContext.cs    System tray UI
  App/DeviceSelectionForm.cs       Device picker dialog
  App/StartupManager.cs            HKCU Run key management
```

## Build

```bash
dotnet build
dotnet run --project src/SilenceSwitch
```

## Key Conventions

- All code in `namespace SilenceSwitch;` (file-scoped, single namespace)
- No third-party packages beyond NAudio — keep dependencies minimal
- PolicyConfigClient.cs: methods must stay in exact vtable order or COM calls will crash
- Catch exceptions in AudioDeviceManager methods — devices can vanish at any time
- Use `System.Windows.Forms.Timer` (not `System.Timers.Timer`) — events fire on the UI thread

## Design Document

See `DESIGN.md` for full architecture, component details, and refinement plan.
