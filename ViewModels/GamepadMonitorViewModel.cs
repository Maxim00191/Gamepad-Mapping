using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public partial class GamepadMonitorViewModel : ObservableObject
{
    private readonly Action<bool>? _setHudEnabled;
    private readonly Action<float, float>? _deadzoneChanged;

    public GamepadMonitorViewModel(
        ICommand stopGamepadCommand,
        ICommand startGamepadCommand,
        Action<bool>? setHudEnabled = null,
        float initialLeftThumbstickDeadzone = 0.10f,
        float initialRightThumbstickDeadzone = 0.10f,
        Action<float, float>? deadzoneChanged = null)
    {
        StopGamepadCommand = stopGamepadCommand;
        StartGamepadCommand = startGamepadCommand;
        _setHudEnabled = setHudEnabled;
        _deadzoneChanged = deadzoneChanged;
        leftThumbstickDeadzone = initialLeftThumbstickDeadzone;
        rightThumbstickDeadzone = initialRightThumbstickDeadzone;
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

    partial void OnIsHudEnabledChanged(bool value)
    {
        _setHudEnabled?.Invoke(value);
    }

    partial void OnLeftThumbstickDeadzoneChanged(float value)
    {
        _deadzoneChanged?.Invoke(value, RightThumbstickDeadzone);
    }

    partial void OnRightThumbstickDeadzoneChanged(float value)
    {
        _deadzoneChanged?.Invoke(LeftThumbstickDeadzone, value);
    }

    public ICommand StopGamepadCommand { get; }

    public ICommand StartGamepadCommand { get; }
}
