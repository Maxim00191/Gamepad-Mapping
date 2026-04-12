using System;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
namespace Gamepad_Mapping.ViewModels;

public partial class AppToastViewModel : ObservableObject, IDisposable
{
    private readonly IAppToastService _appToastService;
    private DispatcherTimer? _autoHideTimer;

    public AppToastViewModel(IAppToastService appToastService)
    {
        _appToastService = appToastService;
        _appToastService.CurrentToastChanged += OnCurrentToastChanged;
    }

    [ObservableProperty]
    private bool isToastVisible;

    [ObservableProperty]
    private string toastTitle = string.Empty;

    [ObservableProperty]
    private string toastMessage = string.Empty;

    [ObservableProperty]
    private bool hasToastMessage;

    [RelayCommand]
    private void DismissToast() => _appToastService.DismissCurrent();

    private void OnCurrentToastChanged(object? sender, AppToastRequest? request)
    {
        StopAutoHideTimer();

        if (request is null)
        {
            IsToastVisible = false;
            ToastTitle = string.Empty;
            ToastMessage = string.Empty;
            HasToastMessage = false;
            return;
        }

        ToastTitle = request.Title;
        var msg = request.Message?.Trim();
        ToastMessage = msg ?? string.Empty;
        HasToastMessage = msg is { Length: > 0 };
        IsToastVisible = true;

        var seconds = ResolveAutoHideSeconds(request.AutoHideSeconds);
        if (seconds > 0)
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(seconds)
            };
            _autoHideTimer.Tick += (_, _) =>
            {
                StopAutoHideTimer();
                _appToastService.DismissCurrent();
            };
            _autoHideTimer.Start();
        }
    }

    private static int ResolveAutoHideSeconds(int? configured)
    {
        if (configured is null)
            return AppToastDefaults.AutoHideSeconds;
        if (configured.Value <= 0)
            return 0;
        return configured.Value;
    }

    private void StopAutoHideTimer()
    {
        if (_autoHideTimer is null)
            return;

        _autoHideTimer.Stop();
        _autoHideTimer = null;
    }

    public void Dispose()
    {
        StopAutoHideTimer();
        _appToastService.CurrentToastChanged -= OnCurrentToastChanged;
    }
}

