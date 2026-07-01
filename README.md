# SavagePadEmu v0.5.0

Emulador de joystick Xbox 360 virtual para Windows 11, basado en ViGEmBus y DirectInput.

## v0.5.0 — Mapeo visual

- Diagrama interactivo de mando Xbox / PlayStation dentro de la pestaña **Mapeo**.
- Hacé clic sobre A/✕, B/○, X/□, Y/△, D-Pad, bumpers, triggers, Share o Options para asignar la entrada física.
- El diagrama refleja botones, gatillos y sticks en tiempo real usando el perfil y las deadzones activas.
- Cada control visual muestra la entrada asignada (`Button 2`, `Axis 0+`, etc.).
- Se mantiene la tabla detallada para configurar ejes individuales, inversión y limpiar asignaciones.
- No cambia el motor de emulación ni los perfiles existentes.

## Requisitos

- Windows 11.
- .NET 8 SDK para compilar.
- ViGEmBus instalado para crear el mando Xbox 360 virtual.

## Compilar

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable queda en:

```text
bin\Release\net8.0-windows\win-x64\publish\SavagePadEmu.exe
```

## Uso

1. Seleccioná el joystick físico.
2. En **Mapeo**, hacé clic en un control gráfico o en `Bind / Set key`.
3. Presioná o mové la entrada física deseada.
4. Probá el resultado en **Test / Drift**.
5. Guardá el perfil e iniciá emulación.

## v0.5.1 startup diagnostics
If the app cannot open, it now shows the exact startup error and writes a detailed log to:
`%LOCALAPPDATA%\SavagePadEmu\startup-error.log`
