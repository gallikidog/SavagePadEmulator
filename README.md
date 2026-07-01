# SavagePadEmulator v1.0.0

Windows 11 DirectInput → virtual Xbox 360 controller mapper.

## New in v1.0
- Built-in default profile based on `Defaults/profile.json`; first launch creates `%LOCALAPPDATA%\SavagePadEmu\Profiles\Default.json`.
- Persistent diagnostic log: `%LOCALAPPDATA%\SavagePadEmu\SavagePadEmu.log`.
- Response curves for sticks and triggers: **Linear**, **Precision**, **Aggressive**, **Smooth**.
- **Auto-detect deadzone**: keep sticks centered for 1.5 seconds; it measures idle noise and saves recommended deadzones.
- Hot-plug refresh: connected DirectInput devices are checked every 1.2 seconds. The UI updates without restarting; selected-device emulation stops safely if that device is unplugged.
- Optional **Use all connected devices** mode: combines compatible DirectInput device inputs into one virtual Xbox controller. Buttons are merged; each analog axis uses the input furthest from center.
- Input loop owns DirectInput sessions through `Input/DirectInputDeviceSet.cs`; persistent logging is handled by `Services/AppLogger.cs`.

## Build
```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Notes
- Install ViGEmBus before using virtual Xbox 360 emulation.
- Profiles and settings are stored under `%LOCALAPPDATA%\SavagePadEmu`.
- For auto deadzone, do not touch either stick during measurement.
