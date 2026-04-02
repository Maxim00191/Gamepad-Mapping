using System;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Core;
using ITimer = GamepadMapperGUI.Interfaces.Core.ITimer;

namespace GamepadMapperGUI.Core;

public sealed class RealTimeProvider : ITimeProvider
{
    public long GetTickCount64() => Environment.TickCount64;

    public ITimer CreateTimer(TimeSpan interval, Action onTick)
    {
        return new DispatcherTimerWrapper(interval, onTick);
    }

    private sealed class DispatcherTimerWrapper : ITimer
    {
        private readonly DispatcherTimer _timer;

        public DispatcherTimerWrapper(TimeSpan interval, Action onTick)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += (_, _) => onTick();
        }

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
        public void Dispose() => _timer.Stop();
    }
}
