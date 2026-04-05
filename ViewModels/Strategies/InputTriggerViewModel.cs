using System;
using System.Collections.Generic;
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

    public void SyncFrom(MappingEntry mapping)
    {
        EditSourceIsCombination = false;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";

        if (mapping.From is not null && mapping.From.Type == GamepadBindingType.Button)
        {
            var raw = mapping.From.Value ?? string.Empty;
            if (ChordResolver.TryParseButtonChord(raw, out var chordButtons, out var reqRt, out var reqLt, out _)
                && (chordButtons.Count > 1 || reqRt || reqLt))
            {
                EditSourceIsCombination = true;
                if (chordButtons.Count >= 2)
                {
                    EditBindingComboButton1 = chordButtons[0].ToString();
                    EditBindingComboButton2 = chordButtons[1].ToString();
                }
                else if (chordButtons.Count == 1)
                {
                    EditBindingComboButton1 = chordButtons[0].ToString();
                }
                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
            }
            else
            {
                EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                        b => string.Equals(b, raw, StringComparison.OrdinalIgnoreCase))
                    ?? (AvailableGamepadButtons.FirstOrDefault() ?? "A");
            }
        }
        else
        {
            EditBindingFromButton = mapping.From?.Value ?? string.Empty;
        }
    }

    public bool ApplyTo(MappingEntry mapping)
    {
        if (EditSourceIsCombination)
        {
            var b1 = EditBindingComboButton1;
            var b2 = EditBindingComboButton2;
            if (string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase)) return false;
            var combo = $"{b1}+{b2}";
            mapping.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = combo };
        }
        else
        {
            var button = (EditBindingFromButton ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(button))
                return false;
            var isKnownSingleButton = AvailableGamepadButtons.Any(
                b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase));
            if (!isKnownSingleButton)
                return false;
            mapping.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button };
        }
        return true;
    }

    public void Clear()
    {
        EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditSourceIsCombination = false;
        EditBindingComboButton1 = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        EditBindingComboButton2 = AvailableGamepadButtons.Skip(1).FirstOrDefault() ?? "B";
    }
}
