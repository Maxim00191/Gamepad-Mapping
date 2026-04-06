using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

/// <summary>
/// ViewModel for managing the input trigger (the 'From' part of a mapping).
/// </summary>
public partial class InputTriggerViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;

    public InputTriggerViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        AvailableGamepadButtons = _mainViewModel.AvailableGamepadButtons;

        // Initialize defaults
        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
    }

    public ObservableCollection<string> AvailableGamepadButtons { get; }

    [ObservableProperty]
    private string _editBindingFromButton = "A";

    [ObservableProperty]
    private bool _editSourceIsCombination;

    [ObservableProperty]
    private string _editBindingComboButton1 = "A";

    [ObservableProperty]
    private string _editBindingComboButton2 = "B";

    /// <summary>True when the trigger-match threshold editor should be visible (includes incomplete LT/RT selections).</summary>
    public bool SourceInvolvesTrigger => GamepadChordInput.ShouldShowTriggerMatchThresholdEditor(BuildPreviewExpression());

    public void SyncFrom(MappingEntry mapping)
    {
        try
        {
            EditSourceIsCombination = false;
            EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
            EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";

            if (mapping.From is not null &&
                mapping.From.Type is GamepadBindingType.LeftTrigger or GamepadBindingType.RightTrigger)
            {
                EditSourceIsCombination = false;
                EditBindingFromButton = mapping.From.Type == GamepadBindingType.LeftTrigger
                    ? nameof(GamepadBindingType.LeftTrigger)
                    : nameof(GamepadBindingType.RightTrigger);
                return;
            }

            if (mapping.From is not null && mapping.From.Type == GamepadBindingType.Button)
            {
                var raw = mapping.From.Value ?? string.Empty;
                if (GamepadChordInput.TryNormalizeButtonExpression(raw, out var normalized))
                {
                    var parts = GamepadChordInput.SplitNormalizedParts(normalized);
                    if (parts.Length >= 3)
                    {
                        EditSourceIsCombination = false;
                        EditBindingFromButton = normalized;
                        return;
                    }

                    if (parts.Length == 2)
                    {
                        EditSourceIsCombination = true;
                        EditBindingComboButton1 = MatchAvailable(parts[0]);
                        EditBindingComboButton2 = MatchAvailable(parts[1]);
                        return;
                    }

                    if (parts.Length == 1)
                    {
                        EditSourceIsCombination = false;
                        EditBindingFromButton = MatchAvailable(parts[0]);
                        return;
                    }
                }

                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                        b => string.Equals(b, raw, StringComparison.OrdinalIgnoreCase))
                    ?? raw;
            }
            else
            {
                EditBindingFromButton = mapping.From?.Value ?? string.Empty;
            }
        }
        finally
        {
            OnPropertyChanged(nameof(SourceInvolvesTrigger));
        }
    }

    public bool ApplyTo(MappingEntry mapping)
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

    public void Clear()
    {
        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditSourceIsCombination = false;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
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

    partial void OnEditSourceIsCombinationChanged(bool value) => OnPropertyChanged(nameof(SourceInvolvesTrigger));

    partial void OnEditBindingFromButtonChanged(string value) => OnPropertyChanged(nameof(SourceInvolvesTrigger));

    partial void OnEditBindingComboButton1Changed(string value) => OnPropertyChanged(nameof(SourceInvolvesTrigger));

    partial void OnEditBindingComboButton2Changed(string value) => OnPropertyChanged(nameof(SourceInvolvesTrigger));
}
