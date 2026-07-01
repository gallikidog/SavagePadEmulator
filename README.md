# SavagePadEmu v0.6.1

Windows 11 DirectInput-to-XInput emulator using ViGEmBus.

## New in v0.6.1: Clean build

- Removes nullable-font warnings from the Test Pad renderer.
- Keeps the same runtime behavior and diagnostics as v0.6.0.

## Build

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is in `bin\Release\net8.0-windows\win-x64\publish\`.
