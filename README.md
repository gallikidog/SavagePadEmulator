# SavagePadEmulator v1.0.2

Hotfix for the v1.0 test-panel input flicker. The device hot-plug watcher now refreshes the joystick list only when a device is actually connected/disconnected, so it no longer recreates the active DirectInput test handle every 1.2 seconds.

## Build

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
