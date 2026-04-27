using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels;

public partial class GamepadMonitorViewModel : ObservableObject, IDisposable
{
    public const double MonitorPanelWidthMin = 180;
    public const double MonitorPanelWidthMax = 520;

    public static double ClampMonitorWidth(double pixels) =>
        Math.Clamp(pixels, MonitorPanelWidthMin, MonitorPanelWidthMax);

    private const float TriggerDeadzoneMinSpan = 0.02f;
    private const double UiRefreshHz = 60.0;

    private readonly Action<bool>? _setHudEnabled;
    private readonly Action<float, float>? _deadzoneChanged;
    private readonly Action<float, float, float, float>? _triggerDeadzonesChanged;
    private readonly Action<int, double>? _comboHudChromeChanged;
    private readonly Action<double>? _templateSwitchHudChanged;
    private readonly object _monitorSnapshotLock = new();
    private GamepadMonitorUiSnapshot _pendingSnapshot;
    private DispatcherTimer? _uiRefreshTimer;
    private readonly Dispatcher _uiDispatcher;
    private readonly IMainShellVisibility? _mainShellVisibility;

    public GamepadMonitorViewModel(
        ICommand stopGamepadCommand,
        ICommand startGamepadCommand,
        Action<bool>? setHudEnabled = null,
        float initialLeftThumbstickDeadzone = 0.10f,
        float initialRightThumbstickDeadzone = 0.10f,
        Action<float, float>? deadzoneChanged = null,
        float initialLeftTriggerInnerDeadzone = 0f,
        float initialLeftTriggerOuterDeadzone = 1f,
        float initialRightTriggerInnerDeadzone = 0f,
        float initialRightTriggerOuterDeadzone = 1f,
        Action<float, float, float, float>? triggerDeadzonesChanged = null,
        int initialComboHudPanelAlpha = 96,
        double initialComboHudShadowOpacity = 0.28,
        Action<int, double>? comboHudChromeChanged = null,
        double initialTemplateSwitchHudSeconds = 3.0,
        Action<double>? templateSwitchHudChanged = null,
        IMainShellVisibility? mainShellVisibility = null,
        Dispatcher? uiDispatcher = null)
    {
        StopGamepadCommand = stopGamepadCommand;
        StartGamepadCommand = startGamepadCommand;
        _setHudEnabled = setHudEnabled;
        _deadzoneChanged = deadzoneChanged;
        _triggerDeadzonesChanged = triggerDeadzonesChanged;
        _comboHudChromeChanged = comboHudChromeChanged;
        _templateSwitchHudChanged = templateSwitchHudChanged;
        _mainShellVisibility = mainShellVisibility;
        _uiDispatcher = uiDispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (_mainShellVisibility is not null)
            _mainShellVisibility.PrimaryShellHiddenToTrayChanged += OnPrimaryShellHiddenToTrayChanged;
        leftThumbstickDeadzone = initialLeftThumbstickDeadzone;
        rightThumbstickDeadzone = initialRightThumbstickDeadzone;
        leftTriggerInnerDeadzone = initialLeftTriggerInnerDeadzone;
        leftTriggerOuterDeadzone = initialLeftTriggerOuterDeadzone;
        rightTriggerInnerDeadzone = initialRightTriggerInnerDeadzone;
        rightTriggerOuterDeadzone = initialRightTriggerOuterDeadzone;
        comboHudPanelAlpha = initialComboHudPanelAlpha;
        comboHudShadowOpacity = initialComboHudShadowOpacity;
        templateSwitchHudSeconds = initialTemplateSwitchHudSeconds;
        _pendingSnapshot = new GamepadMonitorUiSnapshot(0, 0, 0, 0, 0, 0, string.Empty, string.Empty);
    }

    /// <summary>Called from the gamepad polling thread; values are applied to observable properties on <see cref="UiRefreshHz"/>.</summary>
    public void RecordInputFrameSnapshot(InputFrame frame, InputFrameProcessingResult result, float leftDeadzone, float rightDeadzone)
    {
        static float ClampDeadzone(float v, float dz) => MathF.Abs(v) < dz ? 0f : v;

        var pressed = result.PressedButtons.Length > 0 ? result.PressedButtons[^1].ToString() : string.Empty;
        var released = result.ReleasedButtons.Length > 0 ? result.ReleasedButtons[^1].ToString() : string.Empty;

        if (!frame.IsConnected)
        {
            lock (_monitorSnapshotLock)
            {
                _pendingSnapshot = new GamepadMonitorUiSnapshot(0, 0, 0, 0, 0, 0, string.Empty, string.Empty);
            }

            return;
        }

        var snap = new GamepadMonitorUiSnapshot(
            ClampDeadzone(frame.LeftThumbstick.X, leftDeadzone),
            ClampDeadzone(frame.LeftThumbstick.Y, leftDeadzone),
            ClampDeadzone(frame.RightThumbstick.X, rightDeadzone),
            ClampDeadzone(frame.RightThumbstick.Y, rightDeadzone),
            frame.LeftTrigger,
            frame.RightTrigger,
            pressed,
            released);

        lock (_monitorSnapshotLock)
            _pendingSnapshot = snap;
    }

    partial void OnIsGamepadRunningChanged(bool value)
    {
        if (value)
            RefreshUiRefreshTimerForShellState();
        else
            StopUiRefreshTimer();
    }

    private void OnPrimaryShellHiddenToTrayChanged(object? sender, EventArgs e)
    {
        void Apply() => RefreshUiRefreshTimerForShellState();

        if (_uiDispatcher.CheckAccess())
            Apply();
        else
            _uiDispatcher.BeginInvoke(Apply, DispatcherPriority.Normal);
    }

    private void RefreshUiRefreshTimerForShellState()
    {
        if (!IsGamepadRunning)
            return;

        if (_mainShellVisibility?.IsPrimaryShellHiddenToTray == true)
            StopUiRefreshTimer();
        else
            StartUiRefreshTimer();
    }

    private void StartUiRefreshTimer()
    {
        if (_uiRefreshTimer is not null)
            return;

        _uiRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, _uiDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / UiRefreshHz)
        };
        _uiRefreshTimer.Tick += OnUiRefreshTick;
        _uiRefreshTimer.Start();
    }

    private void StopUiRefreshTimer()
    {
        if (_uiRefreshTimer is null)
            return;

        _uiRefreshTimer.Tick -= OnUiRefreshTick;
        _uiRefreshTimer.Stop();
        _uiRefreshTimer = null;
    }

    private void OnUiRefreshTick(object? sender, EventArgs e)
    {
        GamepadMonitorUiSnapshot snap;
        lock (_monitorSnapshotLock)
            snap = _pendingSnapshot;

        LeftThumbX = snap.LeftThumbX;
        LeftThumbY = snap.LeftThumbY;
        RightThumbX = snap.RightThumbX;
        RightThumbY = snap.RightThumbY;
        LeftTrigger = snap.LeftTrigger;
        RightTrigger = snap.RightTrigger;
        LastButtonPressed = snap.LastButtonPressed;
        LastButtonReleased = snap.LastButtonReleased;
    }

    public void Dispose()
    {
        if (_mainShellVisibility is not null)
            _mainShellVisibility.PrimaryShellHiddenToTrayChanged -= OnPrimaryShellHiddenToTrayChanged;
        StopUiRefreshTimer();
    }

    /// <summary>Re-applies idle monitor labels after UI culture changes. Prefer calling when the gamepad reader is stopped.</summary>
    public void RefreshLocalizedIdleMonitorDefaults()
    {
        LastMappedOutput = AppUiLocalization.GetString("GamepadMonitor_LastMappedOutputNone");
        LastMappingStatus = AppUiLocalization.GetString("GamepadMonitor_WaitingForInputStatus");
    }

    [ObservableProperty]
    private bool isGamepadRunning;

    [ObservableProperty]
    private string lastButtonPressed = string.Empty;

    [ObservableProperty]
    private string lastButtonReleased = string.Empty;

    [ObservableProperty]
    private string lastMappedOutput = AppUiLocalization.GetString("GamepadMonitor_LastMappedOutputNone");

    [ObservableProperty]
    private string lastMappingStatus = AppUiLocalization.GetString("GamepadMonitor_WaitingForInputStatus");

    /// <summary>Non-empty when combo HUD preview is suppressed because output dispatch is blocked (targeting / focus / UIPI).</summary>
    [ObservableProperty]
    private string comboHudGateHint = string.Empty;

    [ObservableProperty]
    private float leftThumbX;

    [ObservableProperty]
    private float leftThumbY;

    [ObservableProperty]
    private float rightThumbX;

    [ObservableProperty]
    private float rightThumbY;

    [ObservableProperty]
    private float leftTrigger;

    [ObservableProperty]
    private float rightTrigger;

    [ObservableProperty]
    private bool isHudEnabled = true;

    [ObservableProperty]
    private bool isMonitorExpanderExpanded;

    [ObservableProperty]
    private double monitorPanelWidth = 220;

    [ObservableProperty]
    private float leftThumbstickDeadzone;

    [ObservableProperty]
    private float rightThumbstickDeadzone;

    [ObservableProperty]
    private float leftTriggerInnerDeadzone;

    [ObservableProperty]
    private float leftTriggerOuterDeadzone;

    [ObservableProperty]
    private float rightTriggerInnerDeadzone;

    [ObservableProperty]
    private float rightTriggerOuterDeadzone;

    /// <summary>ARGB alpha for combo HUD panel (typical range 24–200). Saved to app settings.</summary>
    [ObservableProperty]
    private int comboHudPanelAlpha;

    /// <summary>Drop shadow opacity for combo HUD (typical 0.08–0.55).</summary>
    [ObservableProperty]
    private double comboHudShadowOpacity;

    /// <summary>Display duration in seconds for the template-switch HUD (typical 0.5–5.0).</summary>
    [ObservableProperty]
    private double templateSwitchHudSeconds;

    partial void OnIsHudEnabledChanged(bool value)
    {
        _setHudEnabled?.Invoke(value);
    }

    partial void OnComboHudPanelAlphaChanged(int value) =>
        _comboHudChromeChanged?.Invoke(ComboHudPanelAlpha, ComboHudShadowOpacity);

    partial void OnComboHudShadowOpacityChanged(double value) =>
        _comboHudChromeChanged?.Invoke(ComboHudPanelAlpha, ComboHudShadowOpacity);

    partial void OnTemplateSwitchHudSecondsChanged(double value) =>
        _templateSwitchHudChanged?.Invoke(TemplateSwitchHudSeconds);

    partial void OnLeftThumbstickDeadzoneChanged(float value)
    {
        _deadzoneChanged?.Invoke(value, RightThumbstickDeadzone);
    }

    partial void OnRightThumbstickDeadzoneChanged(float value)
    {
        _deadzoneChanged?.Invoke(LeftThumbstickDeadzone, value);
    }

    partial void OnLeftTriggerInnerDeadzoneChanged(float value)
    {
        if (LeftTriggerOuterDeadzone < value + TriggerDeadzoneMinSpan)
            LeftTriggerOuterDeadzone = value + TriggerDeadzoneMinSpan;
        _triggerDeadzonesChanged?.Invoke(
            LeftTriggerInnerDeadzone,
            LeftTriggerOuterDeadzone,
            RightTriggerInnerDeadzone,
            RightTriggerOuterDeadzone);
    }

    partial void OnLeftTriggerOuterDeadzoneChanged(float value)
    {
        if (LeftTriggerInnerDeadzone > value - TriggerDeadzoneMinSpan)
            LeftTriggerInnerDeadzone = Math.Max(0f, value - TriggerDeadzoneMinSpan);
        _triggerDeadzonesChanged?.Invoke(
            LeftTriggerInnerDeadzone,
            LeftTriggerOuterDeadzone,
            RightTriggerInnerDeadzone,
            RightTriggerOuterDeadzone);
    }

    partial void OnRightTriggerInnerDeadzoneChanged(float value)
    {
        if (RightTriggerOuterDeadzone < value + TriggerDeadzoneMinSpan)
            RightTriggerOuterDeadzone = value + TriggerDeadzoneMinSpan;
        _triggerDeadzonesChanged?.Invoke(
            LeftTriggerInnerDeadzone,
            LeftTriggerOuterDeadzone,
            RightTriggerInnerDeadzone,
            RightTriggerOuterDeadzone);
    }

    partial void OnRightTriggerOuterDeadzoneChanged(float value)
    {
        if (RightTriggerInnerDeadzone > value - TriggerDeadzoneMinSpan)
            RightTriggerInnerDeadzone = Math.Max(0f, value - TriggerDeadzoneMinSpan);
        _triggerDeadzonesChanged?.Invoke(
            LeftTriggerInnerDeadzone,
            LeftTriggerOuterDeadzone,
            RightTriggerInnerDeadzone,
            RightTriggerOuterDeadzone);
    }

    public ICommand StopGamepadCommand { get; }

    public ICommand StartGamepadCommand { get; }

    [RelayCommand]
    private void CollapseMonitor() => IsMonitorExpanderExpanded = false;
}
