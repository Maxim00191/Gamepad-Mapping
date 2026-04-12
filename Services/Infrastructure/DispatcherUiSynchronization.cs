using System;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class DispatcherUiSynchronization(Dispatcher dispatcher) : IUiSynchronization
{
    private readonly Dispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    public void Send(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action, DispatcherPriority.Send);
    }
}
