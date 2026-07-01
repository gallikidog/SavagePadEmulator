# SavagePadEmu v0.7.0

Windows 11 DirectInput-to-XInput emulator using ViGEmBus.

## New in v0.7.0: Guided calibration

- Guided capture of stick center for the current mapping.
- Five-second range capture for both sticks.
- Saves center/minimum/maximum values inside the active JSON profile.
- Uses captured stick calibration in the virtual Xbox output and Test Pad.
- Button to reset only the saved stick calibration without deleting mappings or deadzones.

## How to calibrate

1. Select the physical joystick and open **Calibración / Perfiles**.
2. Leave both sticks untouched and click **1. Capturar centro**.
3. Click **2. Capturar recorrido (5s)** and move both sticks fully in every direction until it finishes.
4. The profile is saved automatically.

## Build

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is in `bin\Release\net8.0-windows\win-x64\publish\`.
