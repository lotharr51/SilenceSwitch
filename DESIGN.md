# SilenceSwitch Design Document

## Overview

SilenceSwitch is a Windows 11 system tray application that automatically switches the default audio output device back to the user's preferred device (e.g., headphones) after a configurable period of silence across all audio outputs.

## Requirements

1. **First-run setup**: On first launch, show a device picker so the user can select their preferred audio device.
2. **Startup switch**: On app launch, immediately set the default output to the preferred device.
3. **Silence detection**: Poll audio meters across ALL active render devices. If no sound is detected on any device for N minutes (default 15), switch back to the preferred device.
4. **System tray**: Run as a tray icon with status info and controls.
5. **Auto-start**: Option to register in Windows startup (HKCU Run registry key).
6. **Settings**: Persist all configuration to a JSON file in `%APPDATA%\SilenceSwitch\`.
7. **Single instance**: Prevent multiple copies from running simultaneously.

## Architecture

```
Program.cs                        Entry point, single-instance check, first-run flow
├── Config/
│   └── AppSettings.cs            JSON settings persistence
├── Audio/
│   ├── AudioDeviceManager.cs     Device enumeration, peak metering, default device switching
│   ├── SilenceMonitor.cs         Timer-based silence detection, triggers device switch
│   └── PolicyConfigClient.cs     COM interop for IPolicyConfig (set default endpoint)
└── App/
    ├── TrayApplicationContext.cs  NotifyIcon, context menu, event wiring
    ├── DeviceSelectionForm.cs     WinForms dialog for picking preferred device + settings
    └── StartupManager.cs         HKCU registry Run key management
```

## Current State

All files exist and the project **builds clean** (`dotnet build` — 0 warnings, 0 errors). The code is functionally complete as a first pass. Below are the details for each component, including any refinements or known issues to address.

---

## Component Details

### 1. Program.cs (Entry Point)

**Location**: `src/SilenceSwitch/Program.cs`

**Flow**:
1. `ApplicationConfiguration.Initialize()` — sets up DPI/font defaults
2. Create a named `Mutex("SilenceSwitch_SingleInstance")` — if not new, show message and exit
3. `AppSettings.Load()` — read settings from disk (or create defaults)
4. If `PreferredDeviceId` is null/empty → show `DeviceSelectionForm` as a modal dialog. If cancelled, exit.
5. Create `AudioDeviceManager` and `SilenceMonitor`
6. If `SwitchOnStartup` is true → call `deviceManager.SetDefaultDevice(settings.PreferredDeviceId)`
7. `silenceMonitor.Start()`
8. `Application.Run(new TrayApplicationContext(...))` — enters the message loop

**No changes needed.** This file is complete.

---

### 2. AppSettings.cs (Configuration)

**Location**: `src/SilenceSwitch/Config/AppSettings.cs`

**Settings file**: `%APPDATA%\SilenceSwitch\settings.json`

**Properties**:
| Property | Type | Default | Description |
|---|---|---|---|
| `PreferredDeviceId` | `string?` | `null` | MMDevice ID of the preferred output device |
| `PreferredDeviceName` | `string?` | `null` | Friendly name (for display only) |
| `SilenceTimeoutMinutes` | `int` | `15` | Minutes of silence before switching |
| `PollingIntervalSeconds` | `int` | `10` | How often to check audio levels |
| `SilenceThreshold` | `float` | `0.0001` | Peak value below which audio is "silent" |
| `SwitchOnStartup` | `bool` | `true` | Whether to force-switch on app launch |

**Methods**:
- `Save()` — serializes to JSON with `WriteIndented = true`, creates directory if needed
- `Load()` — deserializes from JSON, returns `new AppSettings()` if file missing or corrupt

**No changes needed.** This file is complete.

---

### 3. PolicyConfigClient.cs (COM Interop)

**Location**: `src/SilenceSwitch/Audio/PolicyConfigClient.cs`

This provides access to the undocumented Windows `IPolicyConfig` COM interface, which is the standard mechanism used by all audio-switching tools (SoundSwitch, AudioSwitcher, NirCmd, etc.) to programmatically change the default audio endpoint.

**Key details**:
- **IPolicyConfig GUID**: `F8679F50-850A-41CF-9C72-430F290290C8` — stable on Windows 7 through Windows 11 24H2+
- **PolicyConfigClient coclass GUID**: `870AF99C-171D-4F9E-AF0D-E63DF40C2BC9`
- The interface has ~12 methods; we only use `SetDefaultEndpoint(string deviceId, ERole role)`
- The other method signatures are declared (with `IntPtr` placeholders for unused params) to maintain correct vtable ordering — **do not remove or reorder them**
- `ERole` enum: `eConsole = 0`, `eMultimedia = 1`, `eCommunications = 2`

**No changes needed.** This file is complete.

---

### 4. AudioDeviceManager.cs (Device Operations)

**Location**: `src/SilenceSwitch/Audio/AudioDeviceManager.cs`

Wraps NAudio's `MMDeviceEnumerator` for all device operations.

**Public API**:

```csharp
public record AudioDevice(string Id, string FriendlyName);

// List all active render (output) devices
List<AudioDevice> GetActiveRenderDevices()

// Get the current default render device ID (returns null if none)
string? GetDefaultRenderDeviceId()

// Get peak audio level for a specific device (0.0 to 1.0)
float GetPeakValue(string deviceId)

// Get the maximum peak level across ALL active render devices
float GetMaxPeakAcrossAllDevices()

// Set the default audio output device for all three roles (console, multimedia, communications)
// Returns true on success
bool SetDefaultDevice(string deviceId)

// Get a device's friendly name by ID
string? GetDeviceName(string deviceId)
```

**Implementation notes**:
- Uses `MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)` for listing
- Uses `device.AudioMeterInformation.MasterPeakValue` for peak metering
- `SetDefaultDevice` calls `IPolicyConfig.SetDefaultEndpoint` three times (once per `ERole`) to fully switch
- All methods catch exceptions and return safe defaults (null, 0, false) — devices can disconnect at any time

**No changes needed.** This file is complete.

---

### 5. SilenceMonitor.cs (Core Logic)

**Location**: `src/SilenceSwitch/Audio/SilenceMonitor.cs`

**Responsibilities**:
- Runs a `System.Windows.Forms.Timer` that fires every `PollingIntervalSeconds`
- Each tick: calls `GetMaxPeakAcrossAllDevices()` to check if any device is producing sound
- If sound detected (peak > threshold): resets `_lastSoundDetected` to now
- If no sound and silence duration >= timeout AND current default != preferred device: switch
- After switching once, sets `_switchedDueToSilence = true` to avoid re-switching every tick
- Fires events for UI updates

**Public API**:

```csharp
// Events
event Action? DeviceSwitched;           // Fired when silence triggers a device switch
event Action<TimeSpan>? SilenceUpdated; // Fired every tick with current silence duration

// Properties
TimeSpan SilenceDuration { get; }
bool IsRunning { get; }

// Methods
void Start();              // Begin monitoring
void Stop();               // Stop monitoring
void ResetSilenceTimer();  // Reset silence counter (e.g., after manual switch)
```

**Key logic in OnTick**:
1. Get max peak across all devices
2. If peak > threshold → reset timer, clear switched flag
3. Fire `SilenceUpdated` event
4. If already switched → return (don't re-switch)
5. If current default IS already the preferred device → return (nothing to do)
6. If silence duration >= timeout → switch and fire `DeviceSwitched`

**No changes needed.** This file is complete.

---

### 6. DeviceSelectionForm.cs (Settings UI)

**Location**: `src/SilenceSwitch/App/DeviceSelectionForm.cs`

A simple WinForms dialog shown on first run and from the tray "Settings..." menu.

**Layout** (450x400 fixed dialog):
- **Label**: "Select your headphone device:"
- **ListBox**: Shows all active render devices by friendly name (e.g., "Headphones (Realtek Audio)")
- **Label + NumericUpDown**: "Switch back after silence (minutes):" — range 1-120, default from settings
- **CheckBox**: "Switch to headphones on app startup"
- **Save / Cancel buttons**

**Behavior**:
- On open: enumerates devices via `AudioDeviceManager`, pre-selects current preferred device if set
- On Save: validates selection, updates `AppSettings` properties, calls `settings.Save()`
- Uses `DialogResult.None` trick to prevent closing if no device selected
- The `AudioDeviceManager` used for enumeration is created and disposed locally (not shared with the main one)

**No changes needed.** This file is complete.

---

### 7. TrayApplicationContext.cs (System Tray)

**Location**: `src/SilenceSwitch/App/TrayApplicationContext.cs`

Inherits `ApplicationContext` to run the app without a visible main form.

**Tray menu items**:
1. **Status line** (disabled) — shows current output device name, "(preferred)" if matched
2. **Silence counter** (disabled) — shows "Silence: Xm Ys / 15m"
3. Separator
4. **Switch to Headphones Now** — immediately switch + reset silence timer
5. **Settings...** — stops monitor, shows `DeviceSelectionForm`, restarts monitor
6. Separator
7. **Run at Startup** — toggles HKCU registry entry, shows checkmark
8. Separator
9. **Exit** — cleans up and exits

**Events wired**:
- `SilenceMonitor.SilenceUpdated` → updates status and silence counter menu items
- `SilenceMonitor.DeviceSwitched` → shows a balloon notification

**Current icon**: Uses `SystemIcons.Application` as placeholder.

**Known improvements to make** (see Refinements section below):
- Replace placeholder icon with a custom embedded icon
- The `SilenceUpdated` event fires on each poll (~every 10s) and updates menu item text — this is fine since menu items update in-place, no allocation pressure

---

### 8. StartupManager.cs (Auto-Start)

**Location**: `src/SilenceSwitch/App/StartupManager.cs`

Manages the `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` registry key.

**Static methods**:
- `IsEnabled()` → checks if "SilenceSwitch" value exists
- `Enable()` → writes `"<exe path>"` (quoted) to the registry value
- `Disable()` → deletes the value (with `throwOnMissingValue: false`)

Uses `Application.ExecutablePath` to get the current EXE path.

**No changes needed.** This file is complete.

---

## Refinements and Future Work

These are improvements that can be made after the initial implementation is verified working. They are **not blocking** — the app is functional without them.

### P1 — Should do

1. **Custom tray icon**: Create or embed a small `.ico` file (a headphone silhouette works well). Set it on the `NotifyIcon`. Consider two icon states: "monitoring" (normal) and "silence detected / about to switch" (different color).

2. **Tray icon tooltip**: Update `_trayIcon.Text` in `OnSilenceUpdated` to show a quick summary like "SilenceSwitch — Silence: 5m / 15m". Max 128 chars.

3. **Handle device disconnection**: If the preferred device disappears (unplugged), gracefully degrade — log or show a balloon, don't crash. The current catch blocks in `AudioDeviceManager` handle this at the method level, but `SilenceMonitor` should also handle `SetDefaultDevice` returning `false`.

4. **Logging**: Add simple file logging to `%APPDATA%\SilenceSwitch\log.txt` (append, with timestamps). Log: app start, device switches, errors. Keep it small — rotate or cap at ~1MB.

### P2 — Nice to have

5. **Per-device silence detection**: Currently checks max peak across ALL devices. Could optionally only monitor the non-preferred device(s) — e.g., if the user switches to speakers, only watch the speaker's meter.

6. **Publish as single-file**: Add to `.csproj`:
   ```xml
   <PublishSingleFile>true</PublishSingleFile>
   <SelfContained>true</SelfContained>
   <RuntimeIdentifier>win-x64</RuntimeIdentifier>
   ```
   This produces a single `.exe` with no dependency on .NET being installed.

7. **Double-click tray icon**: Wire `_trayIcon.DoubleClick` to open Settings.

---

## Build & Run

```bash
# Build
cd SilenceSwitch
dotnet build

# Run
dotnet run --project src/SilenceSwitch

# Publish single-file (optional)
dotnet publish src/SilenceSwitch -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Dependencies

- **.NET 8.0 SDK** (Windows)
- **NAudio 2.2.1** (NuGet) — provides `MMDeviceEnumerator`, `AudioMeterInformation`, CoreAudio wrappers
- **Windows Forms** (built into .NET SDK, enabled via `<UseWindowsForms>true</UseWindowsForms>`)
- **IPolicyConfig** — undocumented COM interface, no external dependency, declared inline

## Settings File Example

```json
{
  "PreferredDeviceId": "{0.0.0.00000000}.{guid-here}",
  "PreferredDeviceName": "Headphones (Realtek(R) Audio)",
  "SilenceTimeoutMinutes": 15,
  "PollingIntervalSeconds": 10,
  "SilenceThreshold": 0.0001,
  "SwitchOnStartup": true
}
```

The device ID is the MMDevice endpoint ID string assigned by Windows. It is stable across reboots but may change if the device is reinstalled.

## Testing

### Manual Testing Checklist

1. **First run**: Delete `%APPDATA%\SilenceSwitch\settings.json`. Launch app. Device picker should appear. Select a device, save. Tray icon should appear.
2. **Startup switch**: Close and relaunch. Verify default output changes to preferred device.
3. **Silence detection**: Switch default output to a different device (via Windows Settings). Don't play any audio. After the timeout, the app should switch back.
4. **Sound resets timer**: While waiting for timeout, play audio briefly. Timer should reset.
5. **Manual switch**: Right-click tray → "Switch to Headphones Now". Should switch immediately.
6. **Settings change**: Right-click tray → "Settings...". Change timeout. Save. Verify new timeout applies.
7. **Run at Startup**: Toggle the menu item. Check `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` for the entry.
8. **Single instance**: Try launching a second instance. Should show a message box and exit.
9. **Device disconnect**: Unplug preferred device. App should not crash. Plug back in; verify it resumes.

### Automated Testing (Future)

The architecture separates concerns cleanly enough to unit test:
- `AppSettings`: test serialization round-trip
- `SilenceMonitor`: inject a mock `AudioDeviceManager`, verify timer logic and event firing
- `StartupManager`: test registry read/write (integration test)

To enable mocking, `AudioDeviceManager` methods could be extracted to an `IAudioDeviceManager` interface. This is a low-priority refactor.
