using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public partial class GamepadMonitorViewModel : ObservableObject
{
    private readonly Action<bool>? _setHudEnabled;

    public GamepadMonitorViewModel(
        ICommand stopGamepadCommand,
        ICommand startGamepadCommand,
        Action<bool>? setHudEnabled = null)
    {
        StopGamepadCommand = stopGamepadCommand;
        StartGamepadCommand = startGamepadCommand;
        _setHudEnabled = setHudEnabled;
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

    partial void OnIsHudEnabledChanged(bool value)
    {
        _setHudEnabled?.Invoke(value);
    }

    public ICommand StopGamepadCommand { get; }

    public ICommand StartGamepadCommand { get; }
}
