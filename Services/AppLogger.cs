using System;
using System.IO;

namespace SavagePadEmu;

public sealed class AppLogger
{
    private readonly string _path;
    private readonly object _sync = new();

    public AppLogger(string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        _path = Path.Combine(appDataDirectory, "SavagePadEmu.log");
    }

    public void Write(string message)
    {
        try
        {
            lock (_sync)
            {
                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never interrupt input emulation.
        }
    }

    public string Path => _path;
}
