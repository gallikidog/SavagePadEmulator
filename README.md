# SavagePadEmu v0.3.0

Emulador de joystick Xbox 360 virtual para Windows 11, basado en ViGEmBus y DirectInput.

## v0.3.0 — Arquitectura y rendimiento

- Código dividido en módulos:
  - `Models/ConfigurationModels.cs`: perfiles, bindings, calibración y catálogo de controles.
  - `Input/InputSnapshot.cs`: lectura y procesamiento eficiente de DirectInput.
  - `Services/ProfileRepository.cs`: guardado/carga JSON atómico.
  - `UI/TestPadView.cs`: vista visual de Test / Drift.
- Eliminadas las asignaciones de arreglos de ejes repetidas dentro del loop de emulación.
- El loop omite reportes ViGEm cuando el estado físico y la configuración no cambiaron.
- Los cambios de mapeo/calibración usan una revisión interna para aplicarse inmediatamente.
- La calibración se intercambia como un snapshot completo, evitando estados parcialmente actualizados entre UI y loop de polling.
- El panel Test / Drift libera el joystick mientras la emulación está activa, evitando competencia por DirectInput.
- Guardado de perfiles/configuración más seguro: se escribe temporalmente y luego se reemplaza el archivo.

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

## Nota de rendimiento

- **1 ms** prioriza respuesta y puede usar más CPU según el dispositivo/driver.
- **2–4 ms** suele ser un buen equilibrio entre consumo y latencia.
