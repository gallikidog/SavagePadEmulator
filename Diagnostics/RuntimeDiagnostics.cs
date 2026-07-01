using System;
using System.Diagnostics;
using System.Threading;

namespace SavagePadEmu;

/// <summary>Lightweight counters used by the diagnostics page. All writes are lock-free.</summary>
public sealed class RuntimeDiagnostics
{
    private long _inputReads;
    private long _virtualReports;
    private long _lastReadTicks;
    private long _lastReadDurationTicks;
    private long _windowStartTicks = Stopwatch.GetTimestamp();
    private long _windowReads;
    private long _windowReports;
    private double _inputHz;
    private double _reportHz;

    public void RecordInputRead(long elapsedTicks)
    {
        Interlocked.Increment(ref _inputReads);
        Interlocked.Increment(ref _windowReads);
        Interlocked.Exchange(ref _lastReadTicks, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _lastReadDurationTicks, elapsedTicks);
    }

    public void RecordVirtualReport()
    {
        Interlocked.Increment(ref _virtualReports);
        Interlocked.Increment(ref _windowReports);
    }

    public RuntimeDiagnosticsSnapshot Snapshot()
    {
        var now = Stopwatch.GetTimestamp();
        var start = Volatile.Read(ref _windowStartTicks);
        var elapsed = (now - start) / (double)Stopwatch.Frequency;
        if (elapsed >= 0.45)
        {
            var reads = Interlocked.Exchange(ref _windowReads, 0);
            var reports = Interlocked.Exchange(ref _windowReports, 0);
            Interlocked.Exchange(ref _windowStartTicks, now);
            _inputHz = reads / elapsed;
            _reportHz = reports / elapsed;
        }

        var lastRead = Volatile.Read(ref _lastReadTicks);
        var ageMs = lastRead == 0 ? -1d : (now - lastRead) * 1000d / Stopwatch.Frequency;
        var readMs = Volatile.Read(ref _lastReadDurationTicks) * 1000d / Stopwatch.Frequency;
        return new RuntimeDiagnosticsSnapshot(
            Volatile.Read(ref _inputReads),
            Volatile.Read(ref _virtualReports),
            _inputHz,
            _reportHz,
            readMs,
            ageMs);
    }
}

public readonly record struct RuntimeDiagnosticsSnapshot(
    long InputReads,
    long VirtualReports,
    double InputHz,
    double ReportHz,
    double LastReadMs,
    double LastInputAgeMs);
