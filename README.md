# SavagePadEmulator

**Español** | [English](#english)

Emulador de mando **DirectInput a Xbox 360 (XInput)** para Windows 11. Está pensado para usar joysticks genéricos o controles DirectInput en juegos que detectan mejor un mando Xbox/XInput.

> SavagePadEmulator crea un mando Xbox 360 virtual mediante ViGEmBus. El joystick físico sigue siendo leído por DirectInput y se transforma según el perfil activo.

## Características

- Mapeo visual e interactivo de botones, D-Pad, sticks y gatillos.
- Etiquetas Xbox / PlayStation para facilitar la configuración.
- Botón **Bind / Set key** para asignar cada entrada desde el joystick físico.
- Panel **Test / Drift** con estado de botones, sticks, gatillos, valores RAW/procesados y aviso de drift.
- Calibración guiada de sticks: captura de centro y recorrido.
- Deadzone, anti-deadzone, sensibilidad, umbral de drift y frecuencia de polling configurables.
- Curvas de respuesta para sticks y gatillos.
- Perfiles JSON múltiples, guardado/carga y perfil predeterminado integrado.
- Asociación opcional de perfiles a ejecutables de juegos y cambio automático de perfil.
- Detección de conexión/desconexión de dispositivos (hot-plug).
- Opción para combinar entradas de varios joysticks conectados.
- Interfaz en Español e Inglés.
- Logs de ejecución para diagnóstico.

## Requisitos

- Windows 11 x64.
- Un joystick/control reconocido por Windows como dispositivo DirectInput.
- **ViGEmBus** instalado y funcionando; es necesario para crear el mando Xbox 360 virtual.

## Instalación

### Opción A: instalador

1. Descargá `SavagePadEmulator-Setup-<versión>-win-x64.exe` desde **Releases**.
2. Ejecutalo y completá el asistente.
3. Instalá ViGEmBus si todavía no está instalado.
4. Abrí SavagePadEmulator desde el acceso directo o el menú Inicio.

### Opción B: versión portable

1. Descargá `SavagePadEmulator-<versión>-portable-win-x64.zip`.
2. Extraé el ZIP en una carpeta con permisos de escritura, por ejemplo `C:\Apps\SavagePadEmulator`.
3. Ejecutá `SavagePadEmu.exe`.
4. No muevas ni borres la carpeta `Defaults`, porque contiene el perfil de primera ejecución.

## Inicio rápido

1. Conectá el joystick físico antes de abrir la aplicación.
2. Elegilo en la lista de dispositivos de la parte superior.
3. Abrí la pestaña **Mapeo**.
4. Presioná **Bind / Set key** junto al control que quieras configurar.
5. Presioná o mové la entrada física correspondiente.
6. Abrí **Test / Drift** y verificá que botones, sticks y gatillos se reflejen correctamente.
7. En **Calibración / Perfiles**, ajustá deadzones o ejecutá la calibración guiada si hay drift.
8. Guardá el perfil y presioná **Iniciar emulación**.

## Mapeo recomendado

La aplicación incluye un perfil predeterminado para controles genéricos con esta disposición principal:

| Salida Xbox | Equivalente PlayStation | Entrada física predeterminada |
|---|---:|---:|
| A | ✕ | Button 2 |
| B | ○ | Button 1 |
| X | □ | Button 3 |
| Y | △ | Button 0 |
| LB | L1 | Button 4 |
| RB | R1 | Button 5 |
| LT | L2 | Button 6 |
| RT | R2 | Button 7 |
| Back | Share | Button 8 |
| Start | Options | Button 9 |
| LS | L3 | Button 10 |
| RS | R3 | Button 11 |

Los sticks y el D-Pad también vienen configurados en el perfil inicial. Cada dispositivo puede informar índices diferentes: usá **Bind / Set key** para adaptarlo a tu joystick.

## Calibración y drift

En **Calibración / Perfiles** podés configurar:

- **Deadzone de stick izquierdo/derecho:** ignora pequeños movimientos alrededor del centro. Para drift leve, empezá entre 6% y 12%.
- **Deadzone de gatillos:** evita activación mínima accidental.
- **Anti-deadzone:** ayuda con juegos que tienen una deadzone interna alta. Usá valores bajos.
- **Sensibilidad:** ajusta la amplitud de salida de los sticks.
- **Curvas de respuesta:** cambian cómo responde el stick entre el centro y el extremo.
- **Polling interval:** valores bajos reducen latencia, pero pueden aumentar levemente el uso de CPU.

### Calibración guiada

1. Dejá los sticks quietos y elegí **Capturar centro**.
2. Elegí **Capturar recorrido**.
3. Durante unos segundos, mové ambos sticks hasta todos los extremos varias veces.
4. Guardá el perfil cuando termine.

## Perfiles y datos de usuario

Los perfiles no se guardan junto al ejecutable. Se almacenan en:

```text
%LOCALAPPDATA%\SavagePadEmu\Profiles
```

La configuración general se guarda en:

```text
%LOCALAPPDATA%\SavagePadEmu\settings.json
```

Los registros de diagnóstico se guardan en:

```text
%LOCALAPPDATA%\SavagePadEmu\SavagePadEmu.log
```

Esto evita que una actualización o reinstalación sobrescriba tus perfiles.

## Perfiles por juego

Podés asociar un perfil a un ejecutable de juego desde **Calibración / Perfiles**. Mientras SavagePadEmulator esté abierto, revisará los procesos activos y cargará el perfil asociado al detectar ese juego.

Ejemplos: `FC26.exe`, `cs2.exe`, `RocketLeague.exe`.

## Diagnóstico y problemas frecuentes

### La emulación no inicia

- Confirmá que ViGEmBus esté instalado.
- Cerrá otros emuladores que puedan estar creando un mando virtual, como x360ce, DS4Windows, reWASD o XOutput.
- Ejecutá SavagePadEmulator y el juego con el mismo nivel de permisos. Por ejemplo, ambos normales o ambos como administrador.

### El juego detecta dos mandos o entradas duplicadas

El juego puede estar leyendo tanto el joystick físico como el mando Xbox virtual. Revisá si el juego permite desactivar DirectInput o usar solo XInput. También cerrá otros programas de emulación.

### Los botones se cortan o parpadean

Actualizá a la versión más reciente. El hot-plug se diseñó para refrescar dispositivos solo cuando realmente se conectan o desconectan, evitando reinicios periódicos de lectura.

### Los sticks se mueven solos

Usá **Test / Drift** para confirmar el comportamiento. Luego aumentá la deadzone o ejecutá la calibración guiada y guardá el perfil.

### El joystick no aparece

- Probalo primero en `joy.cpl` de Windows.
- Reconectalo a otro puerto USB.
- Cerrá software que pueda tomar control exclusivo del dispositivo.
- Reiniciá SavagePadEmulator después de conectarlo.

## Compilar desde código fuente

### Requisitos de desarrollo

- .NET 8 SDK x64.
- Windows 11 x64.
- ViGEmBus para probar la emulación.

### Compilación local

```powershell
cd C:\ruta\a\SavagePadEmulator
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable se genera en:

```text
bin\Release\net8.0-windows\win-x64\publish\SavagePadEmu.exe
```

## Crear paquetes de distribución

### Portable ZIP

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Publish-Portable.ps1 -Version 1.0.3
```

Resultado:

```text
artifacts\SavagePadEmulator-1.0.3-portable-win-x64.zip
```

### Instalador

Instalá Inno Setup y luego ejecutá:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Installer\Build-Installer.ps1
```

Si el script no encuentra Inno Setup, compilá manualmente con la ruta de `ISCC.exe` instalada en tu equipo:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\Installer\SavagePadEmu.iss"
```

Resultado esperado:

```text
artifacts\SavagePadEmulator-Setup-1.0.3-win-x64.exe
```


## Tecnologías

- C# / .NET 8 / Windows Forms.
- SharpDX DirectInput para leer joysticks físicos.
- Nefarius ViGEm Client para enviar la salida a un mando Xbox 360 virtual.
- Inno Setup para el instalador de Windows.

## Contribuciones

Los reportes de errores son más útiles cuando incluyen:

- versión de SavagePadEmulator;
- versión de Windows;
- modelo del joystick;
- pasos para reproducir el problema;
- contenido relevante de `%LOCALAPPDATA%\SavagePadEmu\SavagePadEmu.log`.

<a id="english"></a>

# SavagePadEmulator — English

A **DirectInput-to-Xbox 360 (XInput)** controller emulator for Windows 11. It is designed for generic gamepads and DirectInput controllers that need to work in games with better Xbox/XInput support.

> SavagePadEmulator creates a virtual Xbox 360 controller through ViGEmBus. The physical controller is read through DirectInput and transformed according to the active profile.

## Features

- Interactive visual mapping for buttons, D-Pad, sticks, and triggers.
- Xbox / PlayStation labels to make setup easier.
- **Bind / Set key** action for assigning each target from the physical controller.
- **Test / Drift** panel with live button, stick, trigger, RAW/processed value, and drift feedback.
- Guided stick calibration with center and range capture.
- Configurable deadzones, anti-deadzone, sensitivity, drift threshold, and polling interval.
- Stick and trigger response curves.
- Multiple JSON profiles, save/load actions, and an embedded default profile.
- Optional game executable associations and automatic profile switching.
- Device connection/disconnection detection (hot-plug).
- Option to combine input from multiple connected controllers.
- Spanish and English user interface.
- Persistent diagnostic logs.

## Requirements

- Windows 11 x64.
- A controller recognized by Windows as a DirectInput device.
- **ViGEmBus** installed and working; it is required to create the virtual Xbox 360 controller.

## Installation

### Option A: installer

1. Download `SavagePadEmulator-Setup-<version>-win-x64.exe` from **Releases**.
2. Run it and complete the setup wizard.
3. Install ViGEmBus if it is not already available.
4. Start SavagePadEmulator from its shortcut or the Start menu.

### Option B: portable version

1. Download `SavagePadEmulator-<version>-portable-win-x64.zip`.
2. Extract it to a writable directory, for example `C:\Apps\SavagePadEmulator`.
3. Run `SavagePadEmu.exe`.
4. Do not move or remove the `Defaults` directory, because it contains the first-run profile.

## Quick start

1. Connect the physical controller before opening the application.
2. Select it from the device list at the top.
3. Open the **Mapping** tab.
4. Press **Bind / Set key** beside the control you want to configure.
5. Press or move the matching physical input.
6. Open **Test / Drift** and verify buttons, sticks, and triggers.
7. In **Calibration / Profiles**, adjust deadzones or run guided calibration if drift is present.
8. Save the profile and press **Start emulation**.

## Default mapping

The application includes a default profile for generic controllers with this main layout:

| Xbox output | PlayStation equivalent | Default physical input |
|---|---:|---:|
| A | ✕ | Button 2 |
| B | ○ | Button 1 |
| X | □ | Button 3 |
| Y | △ | Button 0 |
| LB | L1 | Button 4 |
| RB | R1 | Button 5 |
| LT | L2 | Button 6 |
| RT | R2 | Button 7 |
| Back | Share | Button 8 |
| Start | Options | Button 9 |
| LS | L3 | Button 10 |
| RS | R3 | Button 11 |

The D-Pad and sticks are also configured in the initial profile. Devices can report different indexes, so use **Bind / Set key** to adapt the mapping to your controller.

## Calibration and drift

In **Calibration / Profiles** you can configure:

- **Left/right stick deadzone:** ignores small movement around center. For mild drift, start between 6% and 12%.
- **Trigger deadzone:** prevents accidental minor trigger activation.
- **Anti-deadzone:** helps games that apply a high internal deadzone. Use low values.
- **Sensitivity:** adjusts stick output range.
- **Response curves:** change how a stick responds from center to edge.
- **Polling interval:** lower values reduce latency but may slightly increase CPU use.

### Guided calibration

1. Leave the sticks untouched and choose **Capture center**.
2. Choose **Capture range**.
3. For a few seconds, move both sticks repeatedly to every edge.
4. Save the profile when finished.

## Profiles and user data

Profiles are not saved beside the executable. They are stored at:

```text
%LOCALAPPDATA%\SavagePadEmu\Profiles
```

General settings are stored at:

```text
%LOCALAPPDATA%\SavagePadEmu\settings.json
```

Diagnostic logs are stored at:

```text
%LOCALAPPDATA%\SavagePadEmu\SavagePadEmu.log
```

This prevents updates or reinstalls from overwriting your profiles.

## Game profiles

You can associate a profile with a game executable in **Calibration / Profiles**. While SavagePadEmulator is open, it checks active processes and loads the associated profile when that game is detected.

Examples: `FC26.exe`, `cs2.exe`, `RocketLeague.exe`.

## Diagnostics and troubleshooting

### Emulation will not start

- Confirm that ViGEmBus is installed.
- Close other tools that may create virtual controllers, such as x360ce, DS4Windows, reWASD, or XOutput.
- Run SavagePadEmulator and the game at the same privilege level: both normally or both as administrator.

### The game sees two controllers or duplicate input

The game may be reading both the physical controller and the virtual Xbox controller. Check whether the game can disable DirectInput or use XInput only. Also close other emulation tools.

### Buttons flicker or stop briefly

Update to the latest version. Hot-plug refresh is designed to update devices only when a device actually connects or disconnects, avoiding periodic input resets.

### Sticks move on their own

Use **Test / Drift** to confirm the behavior. Then increase the deadzone or run guided calibration and save the profile.

### The controller does not appear

- Test it first in Windows `joy.cpl`.
- Reconnect it to another USB port.
- Close software that may be taking exclusive control of it.
- Restart SavagePadEmulator after connecting it.

## Build from source

### Development requirements

- .NET 8 SDK x64.
- Windows 11 x64.
- ViGEmBus to test emulation.

### Local build

```powershell
cd C:\path\to\SavagePadEmulator
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable is generated at:

```text
bin\Release\net8.0-windows\win-x64\publish\SavagePadEmu.exe
```

## Build distribution packages

### Portable ZIP

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Publish-Portable.ps1 -Version 1.0.3
```

Expected output:

```text
artifacts\SavagePadEmulator-1.0.3-portable-win-x64.zip
```

### Installer

Install Inno Setup, then run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Installer\Build-Installer.ps1
```

If the script cannot locate Inno Setup, compile manually using the path to `ISCC.exe` installed on your system:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\Installer\SavagePadEmu.iss"
```

Expected output:

```text
artifacts\SavagePadEmulator-Setup-1.0.3-win-x64.exe
```


The included GitHub Actions workflow can prepare release artifacts when you push a version tag.

## Technologies

- C# / .NET 8 / Windows Forms.
- SharpDX DirectInput for physical controller input.
- Nefarius ViGEm Client for virtual Xbox 360 controller output.
- Inno Setup for the Windows installer.

## Contributing

Bug reports are most useful when they include:

- SavagePadEmulator version;
- Windows version;
- controller model;
- steps to reproduce the problem;
- relevant content from `%LOCALAPPDATA%\SavagePadEmu\SavagePadEmu.log`.
