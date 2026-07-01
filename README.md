# SavagePadEmu v0.6.0

Windows 11 DirectInput-to-XInput emulator using ViGEmBus.

## New in v0.6.0: Test & Diagnostics

- Raw and calibrated values for both sticks and triggers.
- On-screen deadzone circle and drift-center indicator.
- Input sampling rate (Hz), last input read time, and virtual report rate.
- Virtual controller connection status.
- All diagnostic counters are lock-free and add negligible overhead.

## Build

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is in `bin\Release\net8.0-windows\win-x64\publish\`.
