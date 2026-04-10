using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
using GamepadMapperGUI.Models.State;
using Gamepad_Mapping.ViewModels;

namespace GamepadMapperGUI.Services.Infrastructure;

/// <summary>
/// Implementation of IUiOrchestrator that wraps PresentationOrchestrator and IAppToastService.
/// </summary>
public partial class UiOrchestrator : ObservableObject, IUiOrchestrator
{
    private readonly PresentationOrchestrator _presentation;
    private readonly IAppToastService _toastService;
    private readonly Dispatcher _dispatcher;

    public UiOrchestrator(PresentationOrchestrator presentation, IAppToastService toastService, Dispatcher dispatcher)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _presentation.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
    }

    public AppTargetingState TargetState => _presentation.TargetState;
    public string TargetStatusText => _presentation.TargetStatusText;

    public void UpdateStatus(AppTargetingState state, string statusText)
    {
        _presentation.UpdateStatus(state, statusText);
    }

    public void ShowComboHud(ComboHudContent? content, byte alpha, double shadowOpacity, string placement)
    {
        if (Enum.TryParse<ComboHudPlacement>(placement, true, out var p))
        {
            _presentation.ShowComboHud(content, alpha, shadowOpacity, p);
        }
    }

    public void ShowTemplateSwitchHud(string profileDisplayName, double seconds, byte alpha, double shadowOpacity, string placement, Action onFinished)
    {
        if (Enum.TryParse<ComboHudPlacement>(placement, true, out var p))
        {
            _presentation.ShowTemplateSwitchHud(profileDisplayName, seconds, alpha, shadowOpacity, p, onFinished);
        }
    }

    public void HideAllHuds()
    {
        _presentation.HideAllHuds();
    }

    public void ApplyHudVisuals(byte alpha, double shadowOpacity)
    {
        _presentation.ApplyHudVisuals(alpha, shadowOpacity);
    }

    public void ShowToast(string title, string message, Action? onClosed = null)
    {
        _toastService.Show(new AppToastRequest
        {
            Title = title,
            Message = message,
            AutoHideSeconds = null,
            OnClosed = onClosed,
            InvokeOnClosedWhenExitingApplication = true
        });
    }

    public void Dispose()
    {
        _presentation.Dispose();
    }
}


