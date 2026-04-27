using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Update;
using Gamepad_Mapping.Views;

namespace GamepadMapperGUI.Services.Infrastructure;

public partial class UiOrchestrator : ObservableObject, IUiOrchestrator
{
    private readonly IAppToastService _toastService;
    private readonly Dispatcher _dispatcher;
    private readonly IMainShellVisibility? _mainShellVisibility;
    private ComboHudWindow? _comboHudWindow;
    private TemplateSwitchHudWindow? _templateSwitchHudWindow;
    private DispatcherTimer? _templateSwitchHudTimer;

    [ObservableProperty]
    private string _targetStatusText = AppUiLocalization.GetString("AppStatus_NoTargetOutputSuppressed");

    [ObservableProperty]
    private AppTargetingState _targetState = AppTargetingState.NoTargetSelected;

    [ObservableProperty]
    private bool _isTemplateSwitchHudActive;

    public UiOrchestrator(IAppToastService toastService, Dispatcher dispatcher, IMainShellVisibility? mainShellVisibility = null)
    {
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _mainShellVisibility = mainShellVisibility;
    }

    /// <inheritdoc cref="IUiOrchestrator.UpdateStatus"/>
    public void UpdateStatus(AppTargetingState state, string statusText)
    {
        void Apply()
        {
            if (_mainShellVisibility?.IsPrimaryShellHiddenToTray == true)
                return;

            TargetState = state;
            TargetStatusText = statusText;
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
    }

    public void ShowComboHud(ComboHudContent? content, byte alpha, double shadowOpacity, string placement)
    {
        if (!Enum.TryParse<ComboHudPlacement>(placement, true, out var p))
            p = ComboHudPlacement.BottomRight;

        void Apply()
        {
            if (content is null)
            {
                _comboHudWindow?.HideHud();
                return;
            }

            if (IsTemplateSwitchHudActive)
                return;

            _comboHudWindow ??= new ComboHudWindow();
            _comboHudWindow.ShowHud(content, alpha, shadowOpacity, p);
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
    }

    public void ShowTemplateSwitchHud(string profileDisplayName, double seconds, byte alpha, double shadowOpacity, string placement, Action onFinished)
    {
        if (!Enum.TryParse<ComboHudPlacement>(placement, true, out var p))
            p = ComboHudPlacement.BottomRight;

        void Apply()
        {
            if (_templateSwitchHudTimer != null)
            {
                _templateSwitchHudTimer.Stop();
                _templateSwitchHudTimer = null;
            }

            IsTemplateSwitchHudActive = true;

            _comboHudWindow?.HideHud();

            _templateSwitchHudTimer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(seconds)
            };

            _templateSwitchHudTimer.Tick += (_, _) =>
            {
                _templateSwitchHudTimer?.Stop();
                _templateSwitchHudTimer = null;
                IsTemplateSwitchHudActive = false;
                _templateSwitchHudWindow?.HideHud();
                onFinished?.Invoke();
            };

            var title = AppUiLocalization.GetString("TemplateSwitchHud_Title");
            var line = new ComboHudLine($"→ {profileDisplayName}", null);
            var content = new ComboHudContent(title, new[] { line });

            _templateSwitchHudWindow ??= new TemplateSwitchHudWindow();
            _templateSwitchHudWindow.ShowHud(content, alpha, shadowOpacity, p);
            _templateSwitchHudTimer.Start();
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
    }

    public void HideAllHuds()
    {
        void Apply()
        {
            if (_templateSwitchHudTimer != null)
            {
                _templateSwitchHudTimer.Stop();
                _templateSwitchHudTimer = null;
            }
            IsTemplateSwitchHudActive = false;
            _comboHudWindow?.HideHud();
            _templateSwitchHudWindow?.HideHud();
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
    }

    public void ApplyHudVisuals(byte alpha, double shadowOpacity)
    {
        void Apply()
        {
            if (_comboHudWindow is { IsVisible: true })
                _comboHudWindow.ApplyVisualSettings(alpha, shadowOpacity);
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Background);
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
        _templateSwitchHudTimer?.Stop();
        _comboHudWindow?.Close();
        _templateSwitchHudWindow?.Close();
    }
}
