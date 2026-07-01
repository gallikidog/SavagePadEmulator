using System;
using SharpDX.DirectInput;

namespace SavagePadEmu;

/// <summary>
/// A compact copy of one DirectInput state. It prevents repeated slider/axis array allocation
/// while mapping the same physical state to several virtual controls.
/// </summary>
public readonly struct InputSnapshot
{
    private readonly bool[] _buttons;
    private readonly int _pov;
    private readonly int _x;
    private readonly int _y;
    private readonly int _z;
    private readonly int _rotationX;
    private readonly int _rotationY;
    private readonly int _rotationZ;
    private readonly int _slider0;
    private readonly int _slider1;
    private readonly ulong _fingerprint;

    public InputSnapshot(JoystickState state)
    {
        _buttons = state.Buttons ?? Array.Empty<bool>();
        _pov = state.PointOfViewControllers is { Length: > 0 } povs ? povs[0] : -1;
        _x = state.X;
        _y = state.Y;
        _z = state.Z;
        _rotationX = state.RotationX;
        _rotationY = state.RotationY;
        _rotationZ = state.RotationZ;
        var sliders = state.Sliders;
        _slider0 = sliders is { Length: > 0 } ? sliders[0] : 32768;
        _slider1 = sliders is { Length: > 1 } ? sliders[1] : 32768;

        unchecked
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (uint)_x) * 1099511628211UL;
            hash = (hash ^ (uint)_y) * 1099511628211UL;
            hash = (hash ^ (uint)_z) * 1099511628211UL;
            hash = (hash ^ (uint)_rotationX) * 1099511628211UL;
            hash = (hash ^ (uint)_rotationY) * 1099511628211UL;
            hash = (hash ^ (uint)_rotationZ) * 1099511628211UL;
            hash = (hash ^ (uint)_slider0) * 1099511628211UL;
            hash = (hash ^ (uint)_slider1) * 1099511628211UL;
            hash = (hash ^ (uint)_pov) * 1099511628211UL;
            for (var i = 0; i < _buttons.Length; i++)
                hash = (hash ^ (_buttons[i] ? 1UL : 0UL)) * 1099511628211UL;
            _fingerprint = hash;
        }
    }

    public ulong Fingerprint => _fingerprint;
    public bool IsButtonPressed(int index) => index >= 0 && index < _buttons.Length && _buttons[index];
    public int Pov => _pov;

    public int Axis(int index) => index switch
    {
        0 => _x,
        1 => _y,
        2 => _z,
        3 => _rotationX,
        4 => _rotationY,
        5 => _rotationZ,
        6 => _slider0,
        7 => _slider1,
        _ => 32768
    };
}

public static class InputMapper
{
    public static bool GetDigital(in InputSnapshot state, Binding binding)
    {
        return binding.Kind switch
        {
            SourceKind.Button => state.IsButtonPressed(binding.Index),
            SourceKind.Pov => PovMatches(state.Pov, binding.Index),
            SourceKind.Axis => binding.Invert ? GetAnalog(state, binding) < 20000 : GetAnalog(state, binding) > 45535,
            _ => false
        };
    }

    public static int GetAnalog(in InputSnapshot state, Binding binding)
    {
        return binding.Kind switch
        {
            SourceKind.Axis => binding.Invert ? 65535 - state.Axis(Math.Clamp(binding.Index, 0, 7)) : state.Axis(Math.Clamp(binding.Index, 0, 7)),
            SourceKind.Button => state.IsButtonPressed(binding.Index) ^ binding.Invert ? 65535 : 0,
            _ => 32768
        };
    }

    public static double CalibrateAxis(int value, bool leftStick, CalibrationSettings calibration) =>
        CalibrateAxis(value, null, leftStick, calibration);

    public static double CalibrateAxis(int value, string? target, bool leftStick, CalibrationSettings calibration)
    {
        var center = 32768;
        var minimum = 0;
        var maximum = 65535;

        if (!string.IsNullOrWhiteSpace(target) &&
            calibration.AxisCalibrations is not null &&
            calibration.AxisCalibrations.TryGetValue(target, out var saved))
        {
            center = Math.Clamp(saved.Center, 0, 65535);
            minimum = Math.Clamp(saved.Minimum, 0, center - 1);
            maximum = Math.Clamp(saved.Maximum, center + 1, 65535);
        }

        var normalized = value >= center
            ? (value - center) / Math.Max(1.0, maximum - center)
            : (value - center) / Math.Max(1.0, center - minimum);
        normalized = Math.Clamp(normalized, -1.0, 1.0);

        var sign = Math.Sign(normalized);
        var magnitude = Math.Abs(normalized);
        var deadzone = leftStick ? calibration.LeftStickDeadzone : calibration.RightStickDeadzone;
        if (magnitude <= deadzone) return 0;

        var scaled = (magnitude - deadzone) / Math.Max(0.0001, 1.0 - deadzone);
        scaled = Math.Min(1.0, scaled * Math.Max(0.25, calibration.Sensitivity));
        if (calibration.AntiDeadzone > 0 && scaled > 0)
            scaled = Math.Min(1.0, calibration.AntiDeadzone + scaled * (1.0 - calibration.AntiDeadzone));

        return sign * scaled;
    }

    public static int CalibrateTrigger(int value, CalibrationSettings calibration)
    {
        var normalized = Math.Clamp(value / 65535.0, 0.0, 1.0);
        if (normalized <= calibration.TriggerDeadzone) return 0;
        var scaled = (normalized - calibration.TriggerDeadzone) / Math.Max(0.0001, 1.0 - calibration.TriggerDeadzone);
        return (int)Math.Clamp(Math.Round(scaled * 255.0), 0, 255);
    }

    public static bool PovMatches(int pov, int direction)
    {
        if (pov < 0) return false;
        return direction switch
        {
            0 => pov >= 31500 || pov <= 4500,
            1 => pov >= 4500 && pov <= 13500,
            2 => pov >= 13500 && pov <= 22500,
            3 => pov >= 22500 && pov <= 31500,
            _ => false
        };
    }
}
