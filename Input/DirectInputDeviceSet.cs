using System;
using System.Collections.Generic;
using SharpDX.DirectInput;

namespace SavagePadEmu;

/// <summary>Owns one or more DirectInput devices for the emulation loop.</summary>
public sealed class DirectInputDeviceSet : IDisposable
{
    private readonly DirectInput _directInput = new();
    private readonly List<Joystick> _devices = new();
    private readonly List<InputSnapshot> _snapshots = new();

    public DirectInputDeviceSet(IEnumerable<Guid> instanceGuids)
    {
        foreach (var guid in instanceGuids)
        {
            var joystick = new Joystick(_directInput, guid);
            joystick.Properties.BufferSize = 128;
            joystick.Acquire();
            _devices.Add(joystick);
        }
    }

    public bool TryRead(out InputSnapshot snapshot)
    {
        _snapshots.Clear();
        foreach (var joystick in _devices)
        {
            try
            {
                joystick.Poll();
                _snapshots.Add(new InputSnapshot(joystick.GetCurrentState()));
            }
            catch
            {
                try { joystick.Acquire(); } catch { }
            }
        }

        if (_snapshots.Count == 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = InputSnapshot.Merge(_snapshots);
        return true;
    }

    public void Dispose()
    {
        foreach (var joystick in _devices) joystick.Dispose();
        _directInput.Dispose();
    }
}
