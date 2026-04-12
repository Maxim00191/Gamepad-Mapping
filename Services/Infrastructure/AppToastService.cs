using System.Windows;
using System.Windows.Threading;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class AppToastService : IAppToastService
{
    private readonly Dispatcher _dispatcher;
    private AppToastRequest? _current;

    public AppToastService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public event EventHandler<AppToastRequest?>? CurrentToastChanged;

    public void Show(AppToastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _dispatcher.BeginInvoke(() => ShowCore(request), DispatcherPriority.Normal);
    }

    public void DismissCurrent()
    {
        _dispatcher.BeginInvoke(DismissCore, DispatcherPriority.Normal);
    }

    public void NotifyApplicationExiting()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(NotifyApplicationExiting);
            return;
        }

        if (_current is null)
            return;

        var c = _current;
        _current = null;
        CurrentToastChanged?.Invoke(this, null);

        if (c.InvokeOnClosedWhenExitingApplication)
            c.OnClosed?.Invoke();
    }

    private void ShowCore(AppToastRequest request)
    {
        if (_current is not null)
        {
            var prevClosed = _current.OnClosed;
            _current = null;
            CurrentToastChanged?.Invoke(this, null);
            prevClosed?.Invoke();
        }

        _current = request;
        CurrentToastChanged?.Invoke(this, request);
    }

    private void DismissCore()
    {
        if (_current is null)
            return;

        var cb = _current.OnClosed;
        _current = null;
        CurrentToastChanged?.Invoke(this, null);
        cb?.Invoke();
    }
}


