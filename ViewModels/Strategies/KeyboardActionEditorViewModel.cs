using System;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class KeyboardActionEditorViewModel : ActionEditorViewModelBase
{
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private readonly IEnumerable<KeyboardActionDefinition> _keyboardActions;

    public KeyboardActionEditorViewModel(IKeyboardCaptureService keyboardCaptureService, IEnumerable<KeyboardActionDefinition> keyboardActions)
    {
        _keyboardCaptureService = keyboardCaptureService;
        _keyboardActions = keyboardActions;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeyboardKeyReadOnly))]
    private string _actionId = string.Empty;

    [ObservableProperty]
    private string _keyboardKey = string.Empty;

    [ObservableProperty]
    private string _holdKeyboardKey = string.Empty;

    [ObservableProperty]
    private string _holdThresholdText = string.Empty;

    public bool IsKeyboardKeyReadOnly => !string.IsNullOrWhiteSpace(ActionId);

    [RelayCommand]
    private void RecordKeyboardKey()
    {
        _keyboardCaptureService.BeginCapture(
            "Press a key for the mapping output (Esc to cancel).",
            key =>
            {
                KeyboardKey = key.ToString();
                ActionId = string.Empty;
            });
    }

    [RelayCommand]
    private void RecordHoldKeyboardKey()
    {
        _keyboardCaptureService.BeginCapture(
            "Press the HOLD output key (Esc to cancel).",
            key => HoldKeyboardKey = key.ToString());
    }

    public override void SyncFrom(MappingEntry mapping)
    {
        ActionId = mapping.ActionId ?? string.Empty;
        KeyboardKey = mapping.KeyboardKey ?? string.Empty;
        HoldKeyboardKey = mapping.HoldKeyboardKey ?? string.Empty;
        HoldThresholdText = mapping.HoldThresholdMs?.ToString() ?? string.Empty;
    }

    public override bool ApplyTo(MappingEntry mapping)
    {
        mapping.ItemCycle = null;
        mapping.TemplateToggle = null;
        mapping.RadialMenu = null;

        var actionIdTrimmed = ActionId?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(actionIdTrimmed))
        {
            var def = _keyboardActions.FirstOrDefault(a =>
                string.Equals(a.Id?.Trim(), actionIdTrimmed, StringComparison.OrdinalIgnoreCase));
            if (def == null) return false;

            mapping.ActionId = actionIdTrimmed;
            mapping.ApplyKeyboardCatalogDefinition(def);
        }
        else
        {
            var keyToken = KeyboardKey?.Trim() ?? string.Empty;
            var isMouseLook = MappingEngine.IsMouseLookOutput(keyToken);
            var key = MappingEngine.ParseKey(keyToken);

            if (key == Key.None && !isMouseLook) return false;

            mapping.ActionId = null;
            mapping.KeyboardKey = isMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(keyToken) : key.ToString();
        }

        // Apply hold fields
        var holdToken = HoldKeyboardKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(holdToken))
        {
            mapping.HoldKeyboardKey = string.Empty;
            mapping.HoldThresholdMs = null;
        }
        else
        {
            var holdKey = MappingEngine.ParseKey(holdToken);
            var holdMouseLook = MappingEngine.IsMouseLookOutput(holdToken);
            if (holdKey == Key.None && !holdMouseLook) return false;

            mapping.HoldKeyboardKey = holdMouseLook ? MappingEngine.NormalizeKeyboardKeyToken(holdToken) : holdKey.ToString();
            
            if (int.TryParse(HoldThresholdText, out var ms) && ms > 0)
                mapping.HoldThresholdMs = ms;
            else
                mapping.HoldThresholdMs = null;
        }

        return true;
    }

    public override void Clear()
    {
        ActionId = string.Empty;
        KeyboardKey = string.Empty;
        HoldKeyboardKey = string.Empty;
        HoldThresholdText = string.Empty;
    }
}
