using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public class GamepadMonitorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public GamepadMonitorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public bool IsGamepadRunning => _mainViewModel.IsGamepadRunning;

    public string LastButtonPressed => _mainViewModel.LastButtonPressed;

    public string LastButtonReleased => _mainViewModel.LastButtonReleased;

    public string LastMappedOutput => _mainViewModel.LastMappedOutput;

    public string LastMappingStatus => _mainViewModel.LastMappingStatus;

    public float LeftThumbX => _mainViewModel.LeftThumbX;

    public float LeftThumbY => _mainViewModel.LeftThumbY;

    public float RightThumbX => _mainViewModel.RightThumbX;

    public float RightThumbY => _mainViewModel.RightThumbY;

    public float LeftTrigger => _mainViewModel.LeftTrigger;

    public float RightTrigger => _mainViewModel.RightTrigger;

    public ICommand StopGamepadCommand => _mainViewModel.StopGamepadCommand;

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsGamepadRunning):
                OnPropertyChanged(nameof(IsGamepadRunning));
                break;
            case nameof(MainViewModel.LastButtonPressed):
                OnPropertyChanged(nameof(LastButtonPressed));
                break;
            case nameof(MainViewModel.LastButtonReleased):
                OnPropertyChanged(nameof(LastButtonReleased));
                break;
            case nameof(MainViewModel.LastMappedOutput):
                OnPropertyChanged(nameof(LastMappedOutput));
                break;
            case nameof(MainViewModel.LastMappingStatus):
                OnPropertyChanged(nameof(LastMappingStatus));
                break;
            case nameof(MainViewModel.LeftThumbX):
                OnPropertyChanged(nameof(LeftThumbX));
                break;
            case nameof(MainViewModel.LeftThumbY):
                OnPropertyChanged(nameof(LeftThumbY));
                break;
            case nameof(MainViewModel.RightThumbX):
                OnPropertyChanged(nameof(RightThumbX));
                break;
            case nameof(MainViewModel.RightThumbY):
                OnPropertyChanged(nameof(RightThumbY));
                break;
            case nameof(MainViewModel.LeftTrigger):
                OnPropertyChanged(nameof(LeftTrigger));
                break;
            case nameof(MainViewModel.RightTrigger):
                OnPropertyChanged(nameof(RightTrigger));
                break;
        }
    }
}
