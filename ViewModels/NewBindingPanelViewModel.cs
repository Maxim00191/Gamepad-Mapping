using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public class NewBindingPanelViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public NewBindingPanelViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    public string NewBindingFromButton
    {
        get => _mainViewModel.NewBindingFromButton;
        set => _mainViewModel.NewBindingFromButton = value;
    }

    public TriggerMoment NewBindingTrigger
    {
        get => _mainViewModel.NewBindingTrigger;
        set => _mainViewModel.NewBindingTrigger = value;
    }

    public string NewBindingKeyboardKey
    {
        get => _mainViewModel.NewBindingKeyboardKey;
        set => _mainViewModel.NewBindingKeyboardKey = value;
    }

    public ICommand RecordNewBindingKeyCommand => _mainViewModel.RecordNewBindingKeyCommand;

    public ICommand CreateKeyBindingCommand => _mainViewModel.CreateKeyBindingCommand;

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.AvailableGamepadButtons):
                OnPropertyChanged(nameof(AvailableGamepadButtons));
                break;
            case nameof(MainViewModel.AvailableTriggerModes):
                OnPropertyChanged(nameof(AvailableTriggerModes));
                break;
            case nameof(MainViewModel.NewBindingFromButton):
                OnPropertyChanged(nameof(NewBindingFromButton));
                break;
            case nameof(MainViewModel.NewBindingTrigger):
                OnPropertyChanged(nameof(NewBindingTrigger));
                break;
            case nameof(MainViewModel.NewBindingKeyboardKey):
                OnPropertyChanged(nameof(NewBindingKeyboardKey));
                break;
        }
    }
}
