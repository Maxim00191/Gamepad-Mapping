using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Core;

/// <summary>
/// Short delays that combine <see cref="Task.Delay"/> with spin-wait so sub-millisecond spacing is not always rounded up to the default OS timer resolution.
/// </summary>
internal static class PreciseDelay
{
    public static void DelayBlocking(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (duration <= TimeSpan.Zero)
            return;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingMs = (duration - sw.Elapsed).TotalMilliseconds;
            if (remainingMs > 3)
                Thread.Sleep(Math.Max(1, (int)(remainingMs - 2)));
            else if (remainingMs > 0)
                Thread.SpinWait(50);
        }
    }

    public static async Task DelayAsync(int totalMilliseconds, CancellationToken cancellationToken)
    {
        if (totalMilliseconds <= 0)
            return;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < totalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long remainingMs = totalMilliseconds - sw.ElapsedMilliseconds;
            if (remainingMs > 3)
                await Task.Delay(TimeSpan.FromMilliseconds(remainingMs - 2), cancellationToken).ConfigureAwait(false);
            else if (remainingMs > 0)
                Thread.SpinWait(50);
        }
    }
}
