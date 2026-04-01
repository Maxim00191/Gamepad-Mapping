using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gamepad_Mapping.ViewModels;

public partial class GamepadMonitorViewModel : ObservableObject
{
    private const float TriggerDeadzoneMinSpan = 0.02f;

    private readonly Action<bool>? _setHudEnabled;
    private readonly Action<float, float>? _deadzoneChanged;
    private readonly Action<float, float, float, float>? _triggerDeadzonesChanged;
    private readonly Action<int, double>? _comboHudChromeChanged;

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
        Action<int, double>? comboHudChromeChanged = null)
    {
        StopGamepadCommand = stopGamepadCommand;
        StartGamepadCommand = startGamepadCommand;
        _setHudEnabled = setHudEnabled;
        _deadzoneChanged = deadzoneChanged;
        _triggerDeadzonesChanged = triggerDeadzonesChanged;
        _comboHudChromeChanged = comboHudChromeChanged;
        leftThumbstickDeadzone = initialLeftThumbstickDeadzone;
        rightThumbstickDeadzone = initialRightThumbstickDeadzone;
        leftTriggerInnerDeadzone = initialLeftTriggerInnerDeadzone;
        leftTriggerOuterDeadzone = initialLeftTriggerOuterDeadzone;
        rightTriggerInnerDeadzone = initialRightTriggerInnerDeadzone;
        rightTriggerOuterDeadzone = initialRightTriggerOuterDeadzone;
        comboHudPanelAlpha = initialComboHudPanelAlpha;
        comboHudShadowOpacity = initialComboHudShadowOpacity;
    }

    [ObservableProperty]
    private bool isGamepadRunning;

    [ObservableProperty]
    private string lastButtonPressed = string.Empty;

    [ObservableProperty]
    private string lastButtonReleased = string.Empty;

    [ObservableProperty]
    private string lastMappedOutput = "None";

    [ObservableProperty]
    private string lastMappingStatus = "Waiting for gamepad input";

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

    partial void OnIsHudEnabledChanged(bool value)
    {
        _setHudEnabled?.Invoke(value);
    }

    partial void OnComboHudPanelAlphaChanged(int value) =>
        _comboHudChromeChanged?.Invoke(ComboHudPanelAlpha, ComboHudShadowOpacity);

    partial void OnComboHudShadowOpacityChanged(double value) =>
        _comboHudChromeChanged?.Invoke(ComboHudPanelAlpha, ComboHudShadowOpacity);

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
