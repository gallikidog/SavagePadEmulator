# SavagePadEmulator v1.0.3

Windows 11 DirectInput-to-Xbox 360 emulator with visual mapping, calibration, profiles, diagnostics, response curves, hot-plug support and multi-device input.

## Requirements

- Windows 11 x64
- ViGEmBus installed and working (required for Xbox virtual-controller emulation)

## Portable build

```powershell
.\scripts\Publish-Portable.ps1 -Version 1.0.3
```

## Installer build

Install Inno Setup 6, then run:

```powershell
.\Installer\Build-Installer.ps1
```

See [RELEASE.md](RELEASE.md) for GitHub Release steps.
