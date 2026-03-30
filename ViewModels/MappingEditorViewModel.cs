using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
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

    [ObservableProperty]
    private string editBindingHoldKeyboardKey = string.Empty;

    [ObservableProperty]
    private string editBindingHoldThresholdText = string.Empty;

    [ObservableProperty]
    private bool isCreatingNewMapping;

    [ObservableProperty]
    private bool isMappingDetailsExpanderExpanded = true;

    public string KeyboardKeyCapturePrompt => _mainViewModel.KeyboardCaptureService.KeyboardKeyCapturePrompt;

    private ICommand? _recordKeyboardKeyCommand;
    public ICommand RecordKeyboardKeyCommand => _recordKeyboardKeyCommand ??= new RelayCommand(RecordKeyboardKey);

    private ICommand? _recordHoldKeyboardKeyCommand;
    public ICommand RecordHoldKeyboardKeyCommand => _recordHoldKeyboardKeyCommand ??= new RelayCommand(RecordHoldKeyboardKey);

    private ICommand? _updateSelectedBindingCommand;
    public ICommand UpdateSelectedBindingCommand => _updateSelectedBindingCommand ??= new RelayCommand(UpdateSelectedBinding);

    private ICommand? _addMappingCommand;
    public ICommand AddMappingCommand => _addMappingCommand ??= new RelayCommand(BeginCreateNewMapping);

    private ICommand? _removeSelectedMappingCommand;
    public ICommand RemoveSelectedMappingCommand => _removeSelectedMappingCommand ??= new RelayCommand(RemoveSelectedMapping);

    private ICommand? _saveNewMappingCommand;
    public ICommand SaveNewMappingCommand => _saveNewMappingCommand ??= new RelayCommand(SaveNewMapping);

    private ICommand? _cancelCreateNewMappingCommand;
    public ICommand CancelCreateNewMappingCommand => _cancelCreateNewMappingCommand ??= new RelayCommand(CancelCreateNewMapping);

    public void SyncFromSelection(MappingEntry? value)
    {
        if (value is not null)
            IsCreatingNewMapping = false;

        // Avoid wiping the "new mapping" draft when we clear table selection for create mode.
        if (value is null && IsCreatingNewMapping)
            return;

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
        EditBindingHoldKeyboardKey = value?.HoldKeyboardKey ?? string.Empty;
        EditBindingHoldThresholdText = value?.HoldThresholdMs?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void RecordKeyboardKey()
    {
        if (IsCreatingNewMapping)
        {
            _mainViewModel.KeyboardCaptureService.BeginCapture(
                "Press a key for the new mapping output (Esc to cancel).",
                key =>
                {
                    EditBindingKeyboardKey = key.ToString();
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                });
            return;
        }

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

    private void RecordHoldKeyboardKey()
    {
        if (IsCreatingNewMapping)
        {
            _mainViewModel.KeyboardCaptureService.BeginCapture(
                "Press the HOLD output key (Esc to cancel).",
                key =>
                {
                    EditBindingHoldKeyboardKey = key.ToString();
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                });
            return;
        }

        if (SelectedMapping is null)
            return;

        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press the HOLD output key (Esc to cancel).",
            key =>
            {
                EditBindingHoldKeyboardKey = key.ToString();
                if (SelectedMapping is not null)
                    SelectedMapping.HoldKeyboardKey = EditBindingHoldKeyboardKey;
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

        var holdToken = (EditBindingHoldKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            SelectedMapping.HoldKeyboardKey = string.Empty;
            SelectedMapping.HoldThresholdMs = null;
        }
        else
        {
            var holdKey = MappingEngine.ParseKey(holdToken);
            var holdMouseLook = MappingEngine.IsMouseLookOutput(holdToken);
            if (holdKey == Key.None && !holdMouseLook)
                return;
            SelectedMapping.HoldKeyboardKey = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdToken) : holdKey.ToString();
            int? holdMs = null;
            var t = (EditBindingHoldThresholdText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(t) &&
                int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                parsed > 0)
                holdMs = parsed;
            SelectedMapping.HoldThresholdMs = holdMs;
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BeginCreateNewMapping()
    {
        IsMappingDetailsExpanderExpanded = true;
        IsCreatingNewMapping = true;
        _mainViewModel.SelectedMapping = null;

        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingTrigger = TriggerMoment.Tap;
        EditBindingKeyboardKey = string.Empty;
        EditBindingDescription = string.Empty;
        EditBindingHoldKeyboardKey = string.Empty;
        EditBindingHoldThresholdText = string.Empty;
    }

    private void SaveNewMapping()
    {
        if (!TryBuildMappingFromEditorFields(out var entry))
        {
            MessageBox.Show(
                "Choose a gamepad button from the list and enter a valid keyboard or mouse-look output, then try again.",
                "Cannot save new mapping",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        IsCreatingNewMapping = false;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelCreateNewMapping()
    {
        IsCreatingNewMapping = false;
        SyncFromSelection(SelectedMapping);
    }

    private bool TryBuildMappingFromEditorFields(out MappingEntry entry)
    {
        entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };

        var button = (EditBindingFromButton ?? string.Empty).Trim();
        var keyToken = (EditBindingKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(button))
            return false;

        if (!AvailableGamepadButtons.Any(b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase)))
            return false;

        var key = MappingEngine.ParseKey(keyToken);
        var isMouseLookOutput = MappingEngine.IsMouseLookOutput(keyToken);
        if (key == Key.None && !isMouseLookOutput)
            return false;

        entry.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button };
        entry.Trigger = EditBindingTrigger;
        entry.KeyboardKey = isMouseLookOutput ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString();
        entry.Description = (EditBindingDescription ?? string.Empty).Trim();
        entry.AnalogThreshold = null;

        var holdToken = (EditBindingHoldKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            entry.HoldKeyboardKey = string.Empty;
            entry.HoldThresholdMs = null;
            return true;
        }

        var holdKey = MappingEngine.ParseKey(holdToken);
        var holdMouseLook = MappingEngine.IsMouseLookOutput(holdToken);
        if (holdKey == Key.None && !holdMouseLook)
            return false;

        entry.HoldKeyboardKey = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdToken) : holdKey.ToString();
        int? holdMs = null;
        var t = (EditBindingHoldThresholdText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(t) &&
            int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
            holdMs = parsed;
        entry.HoldThresholdMs = holdMs;
        return true;
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
