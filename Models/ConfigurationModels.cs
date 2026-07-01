using System;
using System.Collections.Generic;
using System.Linq;

namespace SavagePadEmu;

public enum SourceKind
{
    None,
    Button,
    Axis,
    Pov
}

public enum ResponseCurve
{
    Linear,
    Precision,
    Aggressive,
    Smooth
}

public sealed class Binding
{
    public string Target { get; set; } = "";
    public SourceKind Kind { get; set; } = SourceKind.None;
    public int Index { get; set; }
    public bool Invert { get; set; }

    public Binding Clone() => new() { Target = Target, Kind = Kind, Index = Index, Invert = Invert };
}



public sealed class AxisCalibration
{
    public int Center { get; set; } = 32768;
    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 65535;

    public AxisCalibration Clone() => new() { Center = Center, Minimum = Minimum, Maximum = Maximum };
}

public sealed class CalibrationSettings
{
    public double LeftStickDeadzone { get; set; } = 0.08;
    public double RightStickDeadzone { get; set; } = 0.08;
    public double TriggerDeadzone { get; set; } = 0.05;
    public double AntiDeadzone { get; set; }
    public double Sensitivity { get; set; } = 1.00;
    public double DriftWarning { get; set; } = 0.12;
    public int PollIntervalMs { get; set; } = 1;
    /// <summary>Response curve applied after deadzone and before anti-deadzone.</summary>
    public ResponseCurve StickResponseCurve { get; set; } = ResponseCurve.Linear;
    public ResponseCurve TriggerResponseCurve { get; set; } = ResponseCurve.Linear;
    // Values captured by the guided wizard for the mapped stick axes.
    public Dictionary<string, AxisCalibration> AxisCalibrations { get; set; } = new();

    public CalibrationSettings Clone() => new()
    {
        LeftStickDeadzone = LeftStickDeadzone,
        RightStickDeadzone = RightStickDeadzone,
        TriggerDeadzone = TriggerDeadzone,
        AntiDeadzone = AntiDeadzone,
        Sensitivity = Sensitivity,
        DriftWarning = DriftWarning,
        PollIntervalMs = PollIntervalMs,
        StickResponseCurve = StickResponseCurve,
        TriggerResponseCurve = TriggerResponseCurve,
        AxisCalibrations = AxisCalibrations?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()) ?? new()
    };
}

public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public CalibrationSettings Calibration { get; set; } = new();
    public List<Binding> Bindings { get; set; } = new();
}

public sealed class GameProfileAssociation
{
    public string ExecutablePath { get; set; } = "";
    public string ProfilePath { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class AppSettings
{
    public string Language { get; set; } = "es";
    public string ActiveProfilePath { get; set; } = "";
    public List<GameProfileAssociation> GameProfiles { get; set; } = new();
}

public sealed class ProfileEntry
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

public sealed class VirtualTestState
{
    public Dictionary<string, bool> Buttons { get; } = new();
    public double LeftX { get; set; }
    public double LeftY { get; set; }
    public double RightX { get; set; }
    public double RightY { get; set; }
    public int LeftTrigger { get; set; }
    public int RightTrigger { get; set; }

    // Raw DirectInput values for diagnostics. Output values above already include calibration.
    public int LeftXRaw { get; set; } = 32768;
    public int LeftYRaw { get; set; } = 32768;
    public int RightXRaw { get; set; } = 32768;
    public int RightYRaw { get; set; } = 32768;
    public int LeftTriggerRaw { get; set; }
    public int RightTriggerRaw { get; set; }
}


public static class DefaultProfileFactory
{
    /// <summary>Built-in default layout supplied with SavagePadEmulator.</summary>
    public static Profile Create() => new()
    {
        Name = "Default",
        Calibration = new CalibrationSettings
        {
            LeftStickDeadzone = 0.08,
            RightStickDeadzone = 0.08,
            TriggerDeadzone = 0.05,
            AntiDeadzone = 0,
            Sensitivity = 1,
            DriftWarning = 0.12,
            PollIntervalMs = 1,
            StickResponseCurve = ResponseCurve.Linear,
            TriggerResponseCurve = ResponseCurve.Linear,
            AxisCalibrations = new Dictionary<string, AxisCalibration>
            {
                ["LeftStickX"] = new() { Center = 32768, Minimum = 0, Maximum = 65535 },
                ["LeftStickY"] = new() { Center = 32767, Minimum = 0, Maximum = 65535 },
                ["RightStickX"] = new() { Center = 32768, Minimum = 0, Maximum = 65535 },
                ["RightStickY"] = new() { Center = 32767, Minimum = 0, Maximum = 65535 }
            }
        },
        Bindings = new List<Binding>
        {
            new() { Target = "A", Kind = SourceKind.Button, Index = 2 },
            new() { Target = "B", Kind = SourceKind.Button, Index = 1 },
            new() { Target = "X", Kind = SourceKind.Button, Index = 3 },
            new() { Target = "Y", Kind = SourceKind.Button, Index = 0 },
            new() { Target = "LB", Kind = SourceKind.Button, Index = 4 },
            new() { Target = "RB", Kind = SourceKind.Button, Index = 5 },
            new() { Target = "Back", Kind = SourceKind.Button, Index = 8 },
            new() { Target = "Start", Kind = SourceKind.Button, Index = 9 },
            new() { Target = "LS", Kind = SourceKind.Button, Index = 10 },
            new() { Target = "RS", Kind = SourceKind.Button, Index = 11 },
            new() { Target = "DPadUp", Kind = SourceKind.Pov, Index = 0 },
            new() { Target = "DPadRight", Kind = SourceKind.Pov, Index = 1 },
            new() { Target = "DPadDown", Kind = SourceKind.Pov, Index = 2 },
            new() { Target = "DPadLeft", Kind = SourceKind.Pov, Index = 3 },
            new() { Target = "LeftStickX", Kind = SourceKind.Axis, Index = 0 },
            new() { Target = "LeftStickY", Kind = SourceKind.Axis, Index = 1, Invert = true },
            new() { Target = "RightStickX", Kind = SourceKind.Axis, Index = 2 },
            new() { Target = "RightStickY", Kind = SourceKind.Axis, Index = 5, Invert = true },
            new() { Target = "LeftTrigger", Kind = SourceKind.Button, Index = 6 },
            new() { Target = "RightTrigger", Kind = SourceKind.Button, Index = 7 }
        }
    };
}

public static class TargetCatalog
{
    public static readonly string[] All =
    {
        "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "DPadUp", "DPadRight", "DPadDown", "DPadLeft",
        "LeftStickX", "LeftStickY", "RightStickX", "RightStickY", "LeftTrigger", "RightTrigger"
    };

    public static readonly string[] AxisNames = { "X", "Y", "Z", "RotationX", "RotationY", "RotationZ", "Slider0", "Slider1" };
    public static readonly string[] PovNames = { "POV Up", "POV Right", "POV Down", "POV Left" };

    public static bool IsAnalog(string target) => target is "LeftStickX" or "LeftStickY" or "RightStickX" or "RightStickY" or "LeftTrigger" or "RightTrigger";

    public static string Display(string target) => target switch
    {
        "A" => "A (Xbox) / ✕ (PlayStation)",
        "B" => "B (Xbox) / ○ (PlayStation)",
        "X" => "X (Xbox) / □ (PlayStation)",
        "Y" => "Y (Xbox) / △ (PlayStation)",
        "LB" => "LB (Xbox) / L1 (PlayStation)",
        "RB" => "RB (Xbox) / R1 (PlayStation)",
        "LS" => "LS (Xbox) / L3 (PlayStation)",
        "RS" => "RS (Xbox) / R3 (PlayStation)",
        "Back" => "Back (Xbox) / Share (PlayStation)",
        "Start" => "Start (Xbox) / Options (PlayStation)",
        "DPadUp" => "D-Pad Up (Xbox) / ↑ (PlayStation)",
        "DPadRight" => "D-Pad Right (Xbox) / → (PlayStation)",
        "DPadDown" => "D-Pad Down (Xbox) / ↓ (PlayStation)",
        "DPadLeft" => "D-Pad Left (Xbox) / ← (PlayStation)",
        "LeftStickX" => "Left Stick X (Xbox) / Left Stick X (PlayStation)",
        "LeftStickY" => "Left Stick Y (Xbox) / Left Stick Y (PlayStation)",
        "RightStickX" => "Right Stick X (Xbox) / Right Stick X (PlayStation)",
        "RightStickY" => "Right Stick Y (Xbox) / Right Stick Y (PlayStation)",
        "LeftTrigger" => "LT (Xbox) / L2 (PlayStation)",
        "RightTrigger" => "RT (Xbox) / R2 (PlayStation)",
        _ => $"{target} (Xbox) / {target} (PlayStation)"
    };

    public static List<Binding> CreateDefaultBindings() => new()
    {
        new() { Target = "A", Kind = SourceKind.Button, Index = 1 },
        new() { Target = "B", Kind = SourceKind.Button, Index = 2 },
        new() { Target = "X", Kind = SourceKind.Button, Index = 0 },
        new() { Target = "Y", Kind = SourceKind.Button, Index = 3 },
        new() { Target = "LB", Kind = SourceKind.Button, Index = 4 },
        new() { Target = "RB", Kind = SourceKind.Button, Index = 5 },
        new() { Target = "Back", Kind = SourceKind.Button, Index = 6 },
        new() { Target = "Start", Kind = SourceKind.Button, Index = 7 },
        new() { Target = "LS", Kind = SourceKind.Button, Index = 8 },
        new() { Target = "RS", Kind = SourceKind.Button, Index = 9 },
        new() { Target = "DPadUp", Kind = SourceKind.Pov, Index = 0 },
        new() { Target = "DPadRight", Kind = SourceKind.Pov, Index = 1 },
        new() { Target = "DPadDown", Kind = SourceKind.Pov, Index = 2 },
        new() { Target = "DPadLeft", Kind = SourceKind.Pov, Index = 3 },
        new() { Target = "LeftStickX", Kind = SourceKind.Axis, Index = 0 },
        new() { Target = "LeftStickY", Kind = SourceKind.Axis, Index = 1, Invert = true },
        new() { Target = "RightStickX", Kind = SourceKind.Axis, Index = 3 },
        new() { Target = "RightStickY", Kind = SourceKind.Axis, Index = 4, Invert = true },
        new() { Target = "LeftTrigger", Kind = SourceKind.Axis, Index = 2 },
        new() { Target = "RightTrigger", Kind = SourceKind.Axis, Index = 5 }
    };

    public static List<Binding> Normalize(IEnumerable<Binding>? loaded)
    {
        var result = All.Select(target => new Binding { Target = target }).ToList();
        if (loaded is null) return result;

        foreach (var binding in loaded)
        {
            if (string.IsNullOrWhiteSpace(binding.Target)) continue;
            var index = result.FindIndex(item => item.Target == binding.Target);
            if (index >= 0) result[index] = binding.Clone();
        }

        return result;
    }
}
