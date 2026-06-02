# SilenceSwitch

A lightweight Windows 11 system tray app that automatically switches your default audio output back to your preferred device (e.g., headphones) after a configurable period of silence.

## Features

- Monitors audio levels across all output devices
- Switches back to your preferred device after silence timeout (default: 15 minutes)
- Optionally switches to preferred device on startup
- System tray icon with status, manual switch, and settings
- Auto-start with Windows option
- Single-instance enforcement

## Requirements

- Windows 10/11
- .NET 8.0 SDK (for building)

## Quick Start

```bash
dotnet run --project src/SilenceSwitch
```

On first launch, select your preferred audio device from the list. The app minimizes to the system tray.

## Publish as Single EXE

```bash
dotnet publish src/SilenceSwitch -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```
