using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class InputTriggerViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private bool _syncingFromMapping;

    public InputTriggerViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        AvailableGamepadButtons = _mainViewModel.AvailableGamepadButtons;
        foreach (var v in GamepadThumbstickFromValueCatalog.PickList)
            AvailableThumbstickFromValues.Add(v);

        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
        EditThumbstickFromValue = GamepadThumbstickFromValueCatalog.PickList[0];
    }

    public ObservableCollection<string> AvailableGamepadButtons { get; }

    public ObservableCollection<string> AvailableThumbstickFromValues { get; } = [];

    public ObservableCollection<string> AvailableNativeTriggerLabels { get; } =
    [
        nameof(GamepadBindingType.LeftTrigger),
        nameof(GamepadBindingType.RightTrigger)
    ];

    [ObservableProperty]
    private GamepadBindingType _editSourceKind = GamepadBindingType.Button;

    [ObservableProperty]
    private string _editThumbstickFromValue = GamepadThumbstickFromValueCatalog.PickList[0];

    [ObservableProperty]
    private string _editBindingFromButton = "A";

    [ObservableProperty]
    private bool _editSourceIsCombination;

    [ObservableProperty]
    private string _editBindingComboButton1 = "A";

    [ObservableProperty]
    private string _editBindingComboButton2 = "B";

    [ObservableProperty]
    private bool _showSourceKindChangedHint;

    public bool ShowButtonSourceEditor => EditSourceKind == GamepadBindingType.Button;

    public bool ShowThumbstickSourceEditor =>
        EditSourceKind is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick;

    public bool ShowNativeTriggerSourceEditor =>
        EditSourceKind is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger;

    public bool SourceInvolvesTrigger => GamepadChordInput.ShouldShowTriggerMatchThresholdEditor(BuildPreviewExpression());

    public bool ShowDetailsAnalogThreshold =>
        EditSourceKind is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick
        || EditSourceKind is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger
        || (EditSourceKind == GamepadBindingType.Button && SourceInvolvesTrigger);

    public string AnalogThresholdPrimaryCaption =>
        EditSourceKind is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick
            ? T("MappingDetailsAnalogThresholdStickLabel")
            : T("TriggerChordMatchThresholdLabel");

    public string AnalogThresholdSecondaryHint =>
        EditSourceKind is GamepadBindingType.LeftThumbstick or GamepadBindingType.RightThumbstick
            ? T("MappingDetailsAnalogThresholdStickHint")
            : T("TriggerChordMatchThresholdHint");

    public event EventHandler? ConfigurationChanged;

    public void NotifyConfigurationChanged() => ConfigurationChanged?.Invoke(this, EventArgs.Empty);

    public void SyncFrom(MappingEntry mapping)
    {
        _syncingFromMapping = true;
        try
        {
            ShowSourceKindChangedHint = false;
            EditSourceIsCombination = false;
            EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
            EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";

            if (mapping.From is null)
            {
                EditSourceKind = GamepadBindingType.Button;
                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
                EditThumbstickFromValue = GamepadThumbstickFromValueCatalog.PickList[0];
                return;
            }

            EditSourceKind = mapping.From.Type;

            switch (mapping.From.Type)
            {
                case GamepadBindingType.LeftTrigger:
                case GamepadBindingType.RightTrigger:
                    EditSourceIsCombination = false;
                    EditBindingFromButton = mapping.From.Type == GamepadBindingType.LeftTrigger
                        ? nameof(GamepadBindingType.LeftTrigger)
                        : nameof(GamepadBindingType.RightTrigger);
                    break;

                case GamepadBindingType.LeftThumbstick:
                case GamepadBindingType.RightThumbstick:
                    EditSourceIsCombination = false;
                    EditThumbstickFromValue =
                        GamepadThumbstickFromValueCatalog.CanonicalizeForEditor(mapping.From.Value);
                    break;

                case GamepadBindingType.Button:
                    var raw = mapping.From.Value ?? string.Empty;
                    if (GamepadChordInput.TryNormalizeButtonExpression(raw, out var normalized))
                    {
                        var parts = GamepadChordInput.SplitNormalizedParts(normalized);
                        if (parts.Length >= 3)
                        {
                            EditSourceIsCombination = false;
                            EditBindingFromButton = normalized;
                            break;
                        }

                        if (parts.Length == 2)
                        {
                            EditSourceIsCombination = true;
                            EditBindingComboButton1 = MatchAvailable(parts[0]);
                            EditBindingComboButton2 = MatchAvailable(parts[1]);
                            break;
                        }

                        if (parts.Length == 1)
                        {
                            EditSourceIsCombination = false;
                            EditBindingFromButton = MatchAvailable(parts[0]);
                            break;
                        }
                    }

                    EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                            b => string.Equals(b, raw, StringComparison.OrdinalIgnoreCase))
                        ?? raw;
                    break;
            }
        }
        finally
        {
            _syncingFromMapping = false;
            RefreshDerivedTriggerProperties();
        }
    }

    public bool ApplyTo(MappingEntry mapping)
    {
        switch (EditSourceKind)
        {
            case GamepadBindingType.Button:
                return ApplyButtonBinding(mapping);
            case GamepadBindingType.LeftThumbstick:
            case GamepadBindingType.RightThumbstick:
                var stickVal = (EditThumbstickFromValue ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(stickVal))
                    return false;
                mapping.From = new GamepadBinding
                {
                    Type = EditSourceKind,
                    Value = GamepadThumbstickFromValueCatalog.CanonicalizeForEditor(stickVal)
                };
                return true;
            case GamepadBindingType.LeftTrigger:
            case GamepadBindingType.RightTrigger:
                var single = (EditBindingFromButton ?? string.Empty).Trim();
                if (!GamepadChordInput.TryCreateNativeTriggerOnlyBinding(single, out var nativeBinding))
                    return false;
                mapping.From = nativeBinding;
                return true;
            default:
                return false;
        }
    }

    public void Clear()
    {
        EditSourceKind = GamepadBindingType.Button;
        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditSourceIsCombination = false;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
        EditThumbstickFromValue = GamepadThumbstickFromValueCatalog.PickList[0];
        ShowSourceKindChangedHint = false;
        RefreshDerivedTriggerProperties();
    }

    private bool ApplyButtonBinding(MappingEntry mapping)
    {
        if (EditSourceIsCombination)
        {
            var b1 = (EditBindingComboButton1 ?? string.Empty).Trim();
            var b2 = (EditBindingComboButton2 ?? string.Empty).Trim();
            if (string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase))
                return false;

            var rawCombo = $"{b1}+{b2}";
            if (!GamepadChordInput.TryNormalizeButtonExpression(rawCombo, out var normalized))
                return false;

            mapping.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = normalized };
            return true;
        }

        var single = (EditBindingFromButton ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(single))
            return false;

        if (GamepadChordInput.TryCreateNativeTriggerOnlyBinding(single, out var nativeBinding))
        {
            mapping.From = nativeBinding;
            return true;
        }

        if (!GamepadChordInput.TryNormalizeButtonExpression(single, out var normSingle))
            return false;

        mapping.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = normSingle };
        return true;
    }

    private string MatchAvailable(string segment)
    {
        var hit = AvailableGamepadButtons.FirstOrDefault(
            b => string.Equals(b, segment, StringComparison.OrdinalIgnoreCase));
        return hit ?? segment;
    }

    private string BuildPreviewExpression()
    {
        if (EditSourceIsCombination)
            return $"{EditBindingComboButton1 ?? string.Empty}+{EditBindingComboButton2 ?? string.Empty}";
        return EditBindingFromButton ?? string.Empty;
    }

    partial void OnEditSourceKindChanged(GamepadBindingType value)
    {
        if (!_syncingFromMapping)
        {
            ShowSourceKindChangedHint = true;
            switch (value)
            {
                case GamepadBindingType.Button:
                    EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
                    EditSourceIsCombination = false;
                    break;
                case GamepadBindingType.LeftThumbstick:
                case GamepadBindingType.RightThumbstick:
                    EditThumbstickFromValue = GamepadThumbstickFromValueCatalog.PickList[0];
                    EditSourceIsCombination = false;
                    break;
                case GamepadBindingType.LeftTrigger:
                    EditBindingFromButton = nameof(GamepadBindingType.LeftTrigger);
                    EditSourceIsCombination = false;
                    break;
                case GamepadBindingType.RightTrigger:
                    EditBindingFromButton = nameof(GamepadBindingType.RightTrigger);
                    EditSourceIsCombination = false;
                    break;
            }
        }

        OnPropertyChanged(nameof(ShowButtonSourceEditor));
        OnPropertyChanged(nameof(ShowThumbstickSourceEditor));
        OnPropertyChanged(nameof(ShowNativeTriggerSourceEditor));
        OnPropertyChanged(nameof(AnalogThresholdPrimaryCaption));
        OnPropertyChanged(nameof(AnalogThresholdSecondaryHint));
        RefreshDerivedTriggerProperties();
    }

    partial void OnEditThumbstickFromValueChanged(string value) => RefreshDerivedTriggerProperties();

    partial void OnEditSourceIsCombinationChanged(bool value) => RefreshDerivedTriggerProperties();

    partial void OnEditBindingFromButtonChanged(string value) => RefreshDerivedTriggerProperties();

    partial void OnEditBindingComboButton1Changed(string value) => RefreshDerivedTriggerProperties();

    partial void OnEditBindingComboButton2Changed(string value) => RefreshDerivedTriggerProperties();

    private void RefreshDerivedTriggerProperties()
    {
        OnPropertyChanged(nameof(SourceInvolvesTrigger));
        OnPropertyChanged(nameof(ShowDetailsAnalogThreshold));
        OnPropertyChanged(nameof(AnalogThresholdPrimaryCaption));
        OnPropertyChanged(nameof(AnalogThresholdSecondaryHint));

        if (!_syncingFromMapping)
            NotifyConfigurationChanged();
    }

    private static string T(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }
}
