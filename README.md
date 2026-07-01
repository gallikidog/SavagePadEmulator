# SavagePadEmu v0.2

Emulador de joystick Xbox 360 virtual para Windows 11 usando ViGEmBus, SharpDX.DirectInput y WinForms.

## Cambios principales

- Interfaz más moderna y ordenada.
- Mapeo visual estilo x360ce con etiquetas Xbox / PlayStation.
- Botón `Bind / Set key` por cada entrada virtual.
- Panel `Test / Drift` para probar botones, gatillos y sticks.
- Nueva pestaña `Calibración / Perfiles`.
- Deadzone independiente para stick izquierdo y derecho.
- Deadzone para gatillos L2/R2.
- Anti-deadzone y sensibilidad de sticks.
- Umbral configurable para aviso de drift.
- Perfiles JSON con mapeo + calibración.
- Guardado rápido en `profile.json` y guardado manual con `Guardar como...`.
- Compatibilidad ampliada con dispositivos DirectInput `Gamepad` y `Joystick`.
- Loop de emulación optimizado: snapshot de bindings, menos trabajo en UI y polling configurable desde 1 ms.

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

## Uso recomendado

1. Abrí SavagePadEmu.
2. Seleccioná tu joystick físico.
3. Entrá en `Mapeo` y configurá cada botón con `Bind / Set key`.
4. Entrá en `Test / Drift` para confirmar que botones, sticks y gatillos responden bien.
5. Entrá en `Calibración / Perfiles` y ajustá deadzones si tenés drift.
6. Guardá el perfil.
7. Iniciá emulación.

## Notas de input lag

- `Polling interval = 1 ms` busca la menor latencia posible.
- Si notás mucho consumo de CPU, probá `2 ms` o `4 ms`.
- El software evita actualizar la UI dentro del loop principal para reducir overhead.
