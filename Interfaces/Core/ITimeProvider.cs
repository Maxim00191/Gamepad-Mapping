using System;

namespace GamepadMapperGUI.Interfaces.Core;

public interface ITimeProvider
{
    long GetTickCount64();
    ITimer CreateTimer(TimeSpan interval, Action onTick);
}

public interface ITimer : IDisposable
{
    void Start();
    void Stop();
    TimeSpan Interval { get; set; }
}
