using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public partial class GamepadMonitorViewModel : ObservableObject
{
    public GamepadMonitorViewModel(ICommand stopGamepadCommand)
    {
        StopGamepadCommand = stopGamepadCommand;
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

    public ICommand StopGamepadCommand { get; }
}
