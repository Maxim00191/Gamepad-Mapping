using System.Collections.ObjectModel;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

namespace Gamepad_Mapping.ViewModels;

public partial class NewBindingPanelViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public NewBindingPanelViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.PropertyChanged += MainViewModelOnPropertyChanged;
        NewBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        NewBindingTrigger = TriggerMoment.Tap;
    }

    public event EventHandler? ConfigurationChanged;

    public ObservableCollection<string> AvailableGamepadButtons => _mainViewModel.AvailableGamepadButtons;

    public ObservableCollection<TriggerMoment> AvailableTriggerModes => _mainViewModel.AvailableTriggerModes;

    [ObservableProperty]
    private string newBindingFromButton = "A";

    [ObservableProperty]
    private TriggerMoment newBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string newBindingKeyboardKey = string.Empty;

    [ObservableProperty]
    private string newBindingDescription = string.Empty;

    [ObservableProperty]
    private string newBindingHoldKeyboardKey = string.Empty;

    [ObservableProperty]
    private string newBindingHoldThresholdText = string.Empty;

    [ObservableProperty]
    private string newBindingAnalogThresholdText = string.Empty;

    /// <summary>True when the trigger-match threshold editor should be visible.</summary>
    public bool NewBindingSourceInvolvesTrigger =>
        GamepadChordInput.ShouldShowTriggerMatchThresholdEditor((NewBindingFromButton ?? string.Empty).Trim());

    private ICommand? _recordNewBindingKeyCommand;
    public ICommand RecordNewBindingKeyCommand => _recordNewBindingKeyCommand ??= new RelayCommand(RecordNewBindingKey);

    private ICommand? _createKeyBindingCommand;
    public ICommand CreateKeyBindingCommand => _createKeyBindingCommand ??= new RelayCommand(CreateKeyBinding);

    private ICommand? _recordNewHoldKeyCommand;
    public ICommand RecordNewHoldKeyCommand => _recordNewHoldKeyCommand ??= new RelayCommand(RecordNewHoldKey);

    private void RecordNewHoldKey()
    {
        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press the HOLD output key (Esc to cancel).",
            key => NewBindingHoldKeyboardKey = key.ToString());
    }

    private void RecordNewBindingKey()
    {
        _mainViewModel.KeyboardCaptureService.BeginCapture(
            "Press a key for the new key binding (Esc to cancel).",
            key => NewBindingKeyboardKey = key.ToString());
    }

    private void CreateKeyBinding()
    {
        var buttonRaw = (NewBindingFromButton ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(buttonRaw))
            return;

        GamepadBinding fromBinding;
        if (GamepadChordInput.TryCreateNativeTriggerOnlyBinding(buttonRaw, out var nativeFrom))
            fromBinding = nativeFrom;
        else if (GamepadChordInput.TryNormalizeButtonExpression(buttonRaw, out var normalizedFrom))
            fromBinding = new GamepadBinding { Type = GamepadBindingType.Button, Value = normalizedFrom };
        else
            return;

        var keyToken = (NewBindingKeyboardKey ?? string.Empty).Trim();

        var key = MappingEngine.ParseKey(keyToken);
        var isMouseLookOutput = MappingEngine.IsMouseLookOutput(keyToken);
        if (key == Key.None && !isMouseLookOutput)
            return;

        int? holdMs = null;
        var ht = (NewBindingHoldThresholdText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(ht) &&
            int.TryParse(ht, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
            holdMs = parsed;

        var holdTok = (NewBindingHoldKeyboardKey ?? string.Empty).Trim();
        string holdKeyStored = string.Empty;
        if (!string.IsNullOrWhiteSpace(holdTok))
        {
            var hk = MappingEngine.ParseKey(holdTok);
            var holdMouseLook = MappingEngine.IsMouseLookOutput(holdTok);
            if (hk == Key.None && !holdMouseLook)
                return;
            holdKeyStored = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdTok) : hk.ToString();
        }
        else
            holdMs = null;

        float? analogThreshold = null;
        var fromValue = fromBinding.Value ?? string.Empty;
        if (fromBinding.Type is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger ||
            GamepadChordInput.ExpressionInvolvesTrigger(fromValue))
        {
            if (!GamepadChordInput.TryParseTriggerMatchThreshold(NewBindingAnalogThresholdText, out var th))
            {
                MessageBox.Show(
                    Loc("TriggerChordThresholdRequiredMessage"),
                    Loc("MappingEditorSaveFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            analogThreshold = th;
        }

        var entry = new MappingEntry
        {
            From = fromBinding,
            KeyboardKey = isMouseLookOutput ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString(),
            Trigger = NewBindingTrigger,
            Description = (NewBindingDescription ?? string.Empty).Trim(),
            AnalogThreshold = analogThreshold,
            HoldKeyboardKey = holdKeyStored,
            HoldThresholdMs = holdMs
        };

        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Loc(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }

    partial void OnNewBindingFromButtonChanged(string value) =>
        OnPropertyChanged(nameof(NewBindingSourceInvolvesTrigger));

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
        }
    }
}

