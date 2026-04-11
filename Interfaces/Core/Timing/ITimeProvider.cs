using System;

namespace GamepadMapperGUI.Interfaces.Core;

public interface ITimeProvider
{
    long GetTickCount64();

    /// <summary>
    /// Monotonic timestamp from QueryPerformanceCounter (same domain as <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>).
    /// Elapsed seconds between two readings is <c>(b - a) / Stopwatch.Frequency</c>.
    /// </summary>
    long GetPerformanceTimestamp();

    ITimer CreateTimer(TimeSpan interval, Action onTick);
}
