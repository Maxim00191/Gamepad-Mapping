using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels;

public partial class MappingEditorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public MappingEditorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
        _mainViewModel.KeyboardCaptureService.PropertyChanged += KeyboardCaptureServiceOnPropertyChanged;
    }

    public event EventHandler? ConfigurationChanged;

    public ObservableCollection<MappingEntry> Mappings => _mainViewModel.Mappings;

    public MappingEntry? SelectedMapping
    {
        get => _mainViewModel.SelectedMapping;
        set => _mainViewModel.SelectedMapping = value;
    }

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    [ObservableProperty]
    private string editBindingFromButton = "A";

    [ObservableProperty]
    private TriggerMoment editBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string editBindingKeyboardKey = string.Empty;

    [ObservableProperty]
    private string editBindingDescription = string.Empty;

    public string KeyboardKeyCapturePrompt => _mainViewModel.KeyboardCaptureService.KeyboardKeyCapturePrompt;

    private ICommand? _recordKeyboardKeyCommand;
    public ICommand RecordKeyboardKeyCommand => _recordKeyboardKeyCommand ??= new RelayCommand(RecordKeyboardKey);

    private ICommand? _updateSelectedBindingCommand;
    public ICommand UpdateSelectedBindingCommand => _updateSelectedBindingCommand ??= new RelayCommand(UpdateSelectedBinding);

    private ICommand? _addMappingCommand;
    public ICommand AddMappingCommand => _addMappingCommand ??= new RelayCommand(AddMapping);

    private ICommand? _removeSelectedMappingCommand;
    public ICommand RemoveSelectedMappingCommand => _removeSelectedMappingCommand ??= new RelayCommand(RemoveSelectedMapping);

    public void SyncFromSelection(MappingEntry? value)
    {
        if (value?.From is not null && value.From.Type == GamepadBindingType.Button)
        {
            var mappedButton = value.From.Value ?? string.Empty;
            EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                b => string.Equals(b, mappedButton, StringComparison.OrdinalIgnoreCase))
                ?? (AvailableGamepadButtons.FirstOrDefault() ?? "A");
        }
        else
        {
            EditBindingFromButton = value?.From?.Value ?? string.Empty;
        }

        EditBindingTrigger = value?.Trigger ?? TriggerMoment.Tap;
        EditBindingKeyboardKey = value?.KeyboardKey ?? string.Empty;
        EditBindingDescription = value?.Description ?? string.Empty;
    }

    private void RecordKeyboardKey()
    {
        if (SelectedMapping is null)
            return;

        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press a key to assign to the selected mapping (Esc to cancel).",
            key =>
            {
                EditBindingKeyboardKey = key.ToString();
                if (SelectedMapping is not null)
                    SelectedMapping.KeyboardKey = EditBindingKeyboardKey;
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            });
    }

    private void UpdateSelectedBinding()
    {
        if (SelectedMapping is null)
            return;

        var button = (EditBindingFromButton ?? string.Empty).Trim();
        var keyToken = (EditBindingKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(button))
            return;

        var sourceType = SelectedMapping.From?.Type ?? GamepadBindingType.Button;
        if (sourceType == GamepadBindingType.Button &&
            !AvailableGamepadButtons.Any(b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase)))
            return;

        var key = MappingEngine.ParseKey(keyToken);
        var isMouseLookOutput = MappingEngine.IsMouseLookOutput(keyToken);
        if (key == Key.None && !isMouseLookOutput)
            return;

        SelectedMapping.From = new GamepadBinding { Type = sourceType, Value = button };
        SelectedMapping.Trigger = EditBindingTrigger;
        SelectedMapping.KeyboardKey = isMouseLookOutput ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString();
        SelectedMapping.Description = (EditBindingDescription ?? string.Empty).Trim();
        SelectedMapping.AnalogThreshold = null;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddMapping()
    {
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
            KeyboardKey = "A",
            Trigger = TriggerMoment.Tap,
            Description = string.Empty,
            AnalogThreshold = null
        };

        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelectedMapping()
    {
        if (SelectedMapping is null)
            return;

        _mainViewModel.Mappings.Remove(SelectedMapping);
        _mainViewModel.SelectedMapping = _mainViewModel.Mappings.FirstOrDefault();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void KeyboardCaptureServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IKeyboardCaptureService.KeyboardKeyCapturePrompt))
            OnPropertyChanged(nameof(KeyboardKeyCapturePrompt));
    }

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
        }
    }
}
