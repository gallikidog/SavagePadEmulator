# SavagePadEmulator v0.8.0

## New in v0.8
- Multiple JSON profiles.
- Profile selector in **Calibration / Profiles**.
- Create profiles from the app.
- Associate a game `.exe` with the current profile.
- Automatic profile switch when an associated game process is detected.
- Existing `profile.json` remains supported as the default/legacy profile.

## How to use game profiles
1. Go to **Calibration / Profiles**.
2. Create or select a profile and save it.
3. Click **Associate game .exe...** and choose the game executable.
4. Keep SavagePadEmulator open. Every 2 seconds it checks for associated game processes and loads the matching profile.

Profiles created by the app are stored in the `Profiles` folder next to the executable. Associations and the last active profile are saved in `settings.json`.

## Build
```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```


## Profile storage (v0.8.1)
Profiles and settings are now stored in `%LOCALAPPDATA%\SavagePadEmu\Profiles` and `%LOCALAPPDATA%\SavagePadEmu\settings.json`, not next to the executable. Existing `profile.json`, `settings.json`, and `Profiles` data from the old executable folder are automatically copied on first start when possible. Use **Cargar perfil** to open any `.json` profile, or select one from **Calibración / Perfiles**.
