using System;
using System.Threading;
using GamepadMapperGUI.Interfaces.Core;
using ITimer = GamepadMapperGUI.Interfaces.Core.ITimer;

namespace GamepadMapperGUI.Core;

public sealed class RealTimeProvider : ITimeProvider
{
    public long GetTickCount64() => Environment.TickCount64;

    /// <summary>
    /// Input runs on a dedicated polling thread without a WPF message pump; <see cref="System.Windows.Threading.DispatcherTimer"/> never fires there.
    /// </summary>
    public ITimer CreateTimer(TimeSpan interval, Action onTick) =>
        new ThreadPoolOneShotTimer(interval, onTick);

    private sealed class ThreadPoolOneShotTimer : ITimer
    {
        private readonly Action _onTick;
        private Timer? _timer;

        public ThreadPoolOneShotTimer(TimeSpan interval, Action onTick)
        {
            Interval = interval;
            _onTick = onTick;
        }

        public TimeSpan Interval { get; set; }

        public void Start()
        {
            _timer?.Dispose();
            var ms = (int)Math.Clamp(Interval.TotalMilliseconds, 0, int.MaxValue);
            _timer = new Timer(
                _ =>
                {
                    try
                    {
                        _onTick();
                    }
                    catch
                    {
                        // Timer callback must not throw.
                    }
                },
                null,
                dueTime: ms,
                period: Timeout.Infinite);
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
