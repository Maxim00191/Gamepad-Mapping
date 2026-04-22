using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gamepad_Mapping.Models.State;
using Gamepad_Mapping.Utils;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

namespace Gamepad_Mapping.ViewModels.Strategies;

public partial class KeyboardActionEditorViewModel : ActionEditorViewModelBase
{
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private readonly IEnumerable<KeyboardActionDefinition> _keyboardActions;
    private readonly IItemSelectionDialogService _itemSelectionDialogService;
    private readonly IKeyboardActionSelectionBuilder _keyboardActionSelectionBuilder;

    public KeyboardActionEditorViewModel(
        IKeyboardCaptureService keyboardCaptureService,
        IEnumerable<KeyboardActionDefinition> keyboardActions,
        IItemSelectionDialogService itemSelectionDialogService,
        IKeyboardActionSelectionBuilder keyboardActionSelectionBuilder)
    {
        _keyboardCaptureService = keyboardCaptureService;
        _keyboardActions = keyboardActions;
        _itemSelectionDialogService = itemSelectionDialogService;
        _keyboardActionSelectionBuilder = keyboardActionSelectionBuilder;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeyboardKeyReadOnly))]
    [NotifyPropertyChangedFor(nameof(ShowCatalogBehaviorSummary))]
    [NotifyPropertyChangedFor(nameof(CatalogBehaviorSummary))]
    [NotifyPropertyChangedFor(nameof(ShowTapAndHoldKeyEditors))]
    [NotifyPropertyChangedFor(nameof(ActionPickerDisplayText))]
    [NotifyPropertyChangedFor(nameof(ActionIdDisplayText))]
    private string _actionId = string.Empty;

    partial void OnActionIdChanged(string value) => AutoUpdateMapping();

    [ObservableProperty]
    private string _keyboardKey = string.Empty;

    partial void OnKeyboardKeyChanged(string value) => AutoUpdateMapping();

    [ObservableProperty]
    private string _holdKeyboardKey = string.Empty;

    partial void OnHoldKeyboardKeyChanged(string value) => AutoUpdateMapping();

    [ObservableProperty]
    private string _holdThresholdText = string.Empty;

    partial void OnHoldThresholdTextChanged(string value) => AutoUpdateMapping();

    private void AutoUpdateMapping()
    {
        if (_syncingFromMapping) return;
        NotifyConfigurationChanged();
    }

    public bool IsKeyboardKeyReadOnly => !string.IsNullOrWhiteSpace(ActionId);
    public string ActionPickerDisplayText => BuildCurrentActionPickerLabel();
    public string ActionIdDisplayText => string.IsNullOrWhiteSpace(ActionId) ? AppUiLocalization.GetString("MappingCatalogActionId_Empty") : ActionId;

    /// <summary>True when the catalog entry defines a keyboard key (tap/hold editing applies).</summary>
    public bool ShowTapAndHoldKeyEditors
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ActionId))
                return true;
            var def = TryResolveCatalogDefinition();
            return def is null || (!string.IsNullOrWhiteSpace((def.KeyboardKey ?? string.Empty).Trim()) && def.ItemCycle == null && def.RadialMenu == null && def.TemplateToggle == null);
        }
    }

    public bool ShowCatalogBehaviorSummary =>
        !string.IsNullOrWhiteSpace(ActionId) && TryResolveCatalogDefinition() is not null;

    public string CatalogBehaviorSummary
    {
        get
        {
            var def = TryResolveCatalogDefinition();
            if (def is null)
                return string.Empty;

            if (def.RadialMenu is { } rm && !string.IsNullOrWhiteSpace(rm.RadialMenuId))
                return string.Format(AppUiLocalization.GetString("MappingCatalogSummaryRadial"), rm.RadialMenuId.Trim());

            if (def.TemplateToggle is { } tt && !string.IsNullOrWhiteSpace(tt.AlternateProfileId))
                return string.Format(AppUiLocalization.GetString("MappingCatalogSummaryTemplateToggle"), tt.AlternateProfileId.Trim());

            if (def.ItemCycle is { } ic)
            {
                var n = Math.Clamp(ic.SlotCount, 1, 9);
                var dir = ic.Direction == ItemCycleDirection.Previous
                    ? AppUiLocalization.GetString("ItemCycleDirection_Previous")
                    : AppUiLocalization.GetString("ItemCycleDirection_Next");
                return $"{AppUiLocalization.GetString("ActionType_ItemCycle")} (1–{n}, {dir})";
            }

            var key = (def.KeyboardKey ?? string.Empty).Trim();
            if (key.Length > 0)
                return string.Format(AppUiLocalization.GetString("MappingCatalogSummaryKeyboard"), key);

            return AppUiLocalization.GetString("MappingCatalogSummaryEmpty");
        }
    }

    private KeyboardActionDefinition? TryResolveCatalogDefinition()
    {
        var id = ActionId?.Trim() ?? string.Empty;
        if (id.Length == 0)
            return null;

        return _keyboardActions.FirstOrDefault(a =>
            string.Equals((a.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void RecordKeyboardKey()
    {
        _keyboardCaptureService.BeginCapture(
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.MappingOutput),
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
            AppUiLocalization.GetString(AppUiLocalization.KeyboardCapturePromptKeys.MappingHoldOutput),
            key => HoldKeyboardKey = key.ToString());
    }

    [RelayCommand]
    private void PickCatalogAction()
    {
        var selected = _itemSelectionDialogService.Select(
            Application.Current?.MainWindow,
            AppUiLocalization.GetString("KeyboardActionPicker_DialogTitle"),
            AppUiLocalization.GetString("KeyboardActionPicker_SearchPlaceholder"),
            BuildPickerItems(),
            ActionId);
        if (selected is null)
            return;

        ActionId = selected;
    }

    public override void SyncFrom(MappingEntry mapping)
    {
        _syncingFromMapping = true;
        try
        {
            ActionId = mapping.ActionId ?? string.Empty;
            KeyboardKey = mapping.KeyboardKey ?? string.Empty;
            HoldKeyboardKey = mapping.HoldKeyboardKey ?? string.Empty;
            HoldThresholdText = mapping.HoldThresholdMs?.ToString() ?? string.Empty;
        }
        finally
        {
            _syncingFromMapping = false;
        }
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

    public override void OnLocalizationChanged()
    {
        OnPropertyChanged(nameof(ActionPickerDisplayText));
        OnPropertyChanged(nameof(ActionIdDisplayText));
        OnPropertyChanged(nameof(CatalogBehaviorSummary));
    }

    private IReadOnlyList<SelectionDialogItem> BuildPickerItems()
        => _keyboardActionSelectionBuilder.BuildSelectionItems(_keyboardActions);

    private string BuildCurrentActionPickerLabel()
        => _keyboardActionSelectionBuilder.BuildSelectedActionDisplayText(ActionId, _keyboardActions);
}

