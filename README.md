# SavagePadEmu v0.4.0

Emulador de joystick Xbox 360 virtual para Windows 11, basado en ViGEmBus y DirectInput.

## v0.4.0 — Interfaz moderna

- Interfaz renovada con estilo visual moderno y consistente.
- Encabezado de aplicación con identidad SavagePad y controles ordenados.
- Pestañas personalizadas para **Mapeo**, **Test / Drift** y **Calibración / Perfiles**.
- Botones primarios, secundarios y de detener con estados visuales claros.
- Filas de mapeo más legibles: control virtual, entrada asignada, Bind, invertir y limpiar.
- Paneles de calibración y diagnóstico organizados como tarjetas.
- Test Pad con paleta visual más clara y botones activos resaltados.
- No cambia el motor de emulación ni la lógica de perfiles de v0.3.0.

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
2. Configurá los binds y guardá el perfil.
3. Probá sticks/botones en **Test / Drift** antes de iniciar emulación.
4. Ajustá deadzones y polling en **Calibración / Perfiles**.
5. Iniciá emulación.
