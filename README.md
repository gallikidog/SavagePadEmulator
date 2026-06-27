# SavagePadEmu - Visual Mapper + Test Pad + Languages

Emulador básico de joystick Xbox 360 virtual para Windows 11 usando ViGEmBus, SharpDX.DirectInput y WinForms.

## Cambios de esta versión

- Selector de idioma: Español / English.
- Traducciones para botones, pestañas, estado, logs y mensajes principales.
- Guarda el idioma elegido en `settings.json` junto al ejecutable.
- Mantiene `profile.json` para el mapeo del joystick.
- Test Pad visual con sticks, botones, gatillos y detección básica de drift.

## Compilar

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable queda en:

```text
bin\Release\net8.0-windows\win-x64\publish\SavagePadEmu.exe
```

## Requisitos

- Windows 11
- .NET 8 SDK para compilar
- ViGEmBus instalado para crear el mando Xbox 360 virtual
