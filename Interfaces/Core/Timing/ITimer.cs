using System;

namespace GamepadMapperGUI.Interfaces.Core;

public interface ITimer : IDisposable
{
    void Start();
    void Stop();
    TimeSpan Interval { get; set; }
}
