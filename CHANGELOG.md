# Changelog

## v1.0.0
- Built-in default profile, persistent logs, response curves, automatic deadzone measurement.
- DirectInput hot-plug refresh and optional combined input from all connected devices.
- Input-device ownership extracted into `Input/DirectInputDeviceSet.cs`.

## v1.0.2
- Fixed periodic input flicker in Test / Drift caused by the hot-plug watcher rebuilding the device selector.
- Preserve the active test DirectInput device when the selected controller has not changed.
