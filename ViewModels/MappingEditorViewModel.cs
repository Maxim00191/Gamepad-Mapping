using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public class MappingEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public MappingEditorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
    }

    public ObservableCollection<MappingEntry> Mappings => _mainViewModel.Mappings;

    public MappingEntry? SelectedMapping
    {
        get => _mainViewModel.SelectedMapping;
        set => _mainViewModel.SelectedMapping = value;
    }

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    public string EditBindingFromButton
    {
        get => _mainViewModel.EditBindingFromButton;
        set => _mainViewModel.EditBindingFromButton = value;
    }

    public TriggerMoment EditBindingTrigger
    {
        get => _mainViewModel.EditBindingTrigger;
        set => _mainViewModel.EditBindingTrigger = value;
    }

    public string EditBindingKeyboardKey
    {
        get => _mainViewModel.EditBindingKeyboardKey;
        set => _mainViewModel.EditBindingKeyboardKey = value;
    }

    public string KeyboardKeyCapturePrompt => _mainViewModel.KeyboardKeyCapturePrompt;

    public ICommand RecordKeyboardKeyCommand => _mainViewModel.RecordKeyboardKeyCommand;

    public ICommand UpdateSelectedBindingCommand => _mainViewModel.UpdateSelectedBindingCommand;

    public ICommand AddMappingCommand => _mainViewModel.AddMappingCommand;

    public ICommand RemoveSelectedMappingCommand => _mainViewModel.RemoveSelectedMappingCommand;

    private void MainViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Mappings):
                OnPropertyChanged(nameof(Mappings));
                break;
            case nameof(MainViewModel.SelectedMapping):
                OnPropertyChanged(nameof(SelectedMapping));
                break;
            case nameof(MainViewModel.AvailableGamepadButtons):
                OnPropertyChanged(nameof(AvailableGamepadButtons));
                break;
            case nameof(MainViewModel.AvailableTriggerModes):
                OnPropertyChanged(nameof(AvailableTriggerModes));
                break;
            case nameof(MainViewModel.EditBindingFromButton):
                OnPropertyChanged(nameof(EditBindingFromButton));
                break;
            case nameof(MainViewModel.EditBindingTrigger):
                OnPropertyChanged(nameof(EditBindingTrigger));
                break;
            case nameof(MainViewModel.EditBindingKeyboardKey):
                OnPropertyChanged(nameof(EditBindingKeyboardKey));
                break;
            case nameof(MainViewModel.KeyboardKeyCapturePrompt):
                OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
                break;
        }
    }
}
