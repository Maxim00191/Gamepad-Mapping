#nullable enable

using System.Globalization;
using System.Windows;
using Gamepad_Mapping.Models.State;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationOutputActionSelectionService : IAutomationOutputActionSelectionService
{
    private const string KeyboardNoneOptionKey = "AutomationOutputActionPicker_Keyboard_None";
    private const string MouseNoneOptionKey = "AutomationOutputActionPicker_Mouse_None";
    private const string KeyboardDialogTitleKey = "AutomationOutputActionPicker_Keyboard_DialogTitle";
    private const string MouseDialogTitleKey = "AutomationOutputActionPicker_Mouse_DialogTitle";
    private const string KeyboardSearchPlaceholderKey = "AutomationOutputActionPicker_Keyboard_SearchPlaceholder";
    private const string MouseSearchPlaceholderKey = "AutomationOutputActionPicker_Mouse_SearchPlaceholder";

    private static readonly IReadOnlyList<AutomationMouseOutputActionDefinition> MouseActions =
    [
        new("mouse.left.click", "click", "left", "AutomationOutputActionPicker_Mouse_LeftClick"),
        new("mouse.left.press", "press", "left", "AutomationOutputActionPicker_Mouse_LeftPress"),
        new("mouse.left.release", "release", "left", "AutomationOutputActionPicker_Mouse_LeftRelease"),
        new("mouse.left.hold", "hold", "left", "AutomationOutputActionPicker_Mouse_LeftHold"),
        new("mouse.right.click", "click", "right", "AutomationOutputActionPicker_Mouse_RightClick"),
        new("mouse.right.press", "press", "right", "AutomationOutputActionPicker_Mouse_RightPress"),
        new("mouse.right.release", "release", "right", "AutomationOutputActionPicker_Mouse_RightRelease"),
        new("mouse.right.hold", "hold", "right", "AutomationOutputActionPicker_Mouse_RightHold"),
        new("mouse.middle.click", "click", "middle", "AutomationOutputActionPicker_Mouse_MiddleClick"),
        new("mouse.middle.press", "press", "middle", "AutomationOutputActionPicker_Mouse_MiddlePress"),
        new("mouse.middle.release", "release", "middle", "AutomationOutputActionPicker_Mouse_MiddleRelease"),
        new("mouse.middle.hold", "hold", "middle", "AutomationOutputActionPicker_Mouse_MiddleHold")
    ];

    private readonly IReadOnlyList<KeyboardActionDefinition> _keyboardActions;
    private readonly IItemSelectionDialogService _itemSelectionDialogService;
    private readonly IKeyboardActionSelectionBuilder _keyboardActionSelectionBuilder;

    public AutomationOutputActionSelectionService(
        IReadOnlyList<KeyboardActionDefinition> keyboardActions,
        IItemSelectionDialogService itemSelectionDialogService,
        IKeyboardActionSelectionBuilder keyboardActionSelectionBuilder)
    {
        _keyboardActions = keyboardActions;
        _itemSelectionDialogService = itemSelectionDialogService;
        _keyboardActionSelectionBuilder = keyboardActionSelectionBuilder;
    }

    public string BuildKeyboardActionPickerDisplayText(string? actionId)
    {
        var id = (actionId ?? string.Empty).Trim();
        if (id.Length == 0)
            return AppUiLocalization.GetString(KeyboardNoneOptionKey);

        return _keyboardActionSelectionBuilder.BuildSelectedActionDisplayText(id, _keyboardActions);
    }

    public string BuildMouseActionPickerDisplayText(string? actionId)
    {
        var id = (actionId ?? string.Empty).Trim();
        if (id.Length == 0)
            return AppUiLocalization.GetString(MouseNoneOptionKey);

        var match = MouseActions.FirstOrDefault(x =>
            string.Equals(x.ActionId, id, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? id
            : AppUiLocalization.GetString(match.LabelResourceKey);
    }

    public string? PickKeyboardActionId(Window? owner, string? initiallySelectedActionId) =>
        _itemSelectionDialogService.Select(
            owner,
            AppUiLocalization.GetString(KeyboardDialogTitleKey),
            AppUiLocalization.GetString(KeyboardSearchPlaceholderKey),
            BuildKeyboardItems(),
            initiallySelectedActionId);

    public string? PickMouseActionId(Window? owner, string? initiallySelectedActionId) =>
        _itemSelectionDialogService.Select(
            owner,
            AppUiLocalization.GetString(MouseDialogTitleKey),
            AppUiLocalization.GetString(MouseSearchPlaceholderKey),
            BuildMouseItems(),
            initiallySelectedActionId);

    public bool TryResolveKeyboardAction(string? actionId, out string resolvedKeyboardKey)
    {
        resolvedKeyboardKey = string.Empty;
        var id = (actionId ?? string.Empty).Trim();
        if (id.Length == 0)
            return false;

        var match = _keyboardActions.FirstOrDefault(action =>
            string.Equals((action.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return false;

        var key = (match.KeyboardKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        resolvedKeyboardKey = key;
        return true;
    }

    public bool TryResolveMouseAction(string? actionId, out AutomationMouseOutputActionDefinition resolvedAction)
    {
        var id = (actionId ?? string.Empty).Trim();
        var match = MouseActions.FirstOrDefault(x =>
            string.Equals(x.ActionId, id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            resolvedAction = new AutomationMouseOutputActionDefinition(string.Empty, string.Empty, string.Empty, string.Empty);
            return false;
        }

        resolvedAction = match;
        return true;
    }

    private IReadOnlyList<SelectionDialogItem> BuildKeyboardItems()
    {
        var manualLabel = AppUiLocalization.GetString(KeyboardNoneOptionKey);
        var items = new List<SelectionDialogItem>
        {
            new(string.Empty, manualLabel, string.Empty, manualLabel)
        };
        items.AddRange(_keyboardActionSelectionBuilder.BuildSelectionItems(_keyboardActions).Where(i =>
            !string.IsNullOrWhiteSpace(i.Key)));
        return items;
    }

    private static IReadOnlyList<SelectionDialogItem> BuildMouseItems()
    {
        var noneLabel = AppUiLocalization.GetString(MouseNoneOptionKey);
        var optionFormat = AppUiLocalization.GetString("KeyboardActionPicker_OptionFormat");
        var culture = CultureInfo.CurrentUICulture;
        var items = new List<SelectionDialogItem>
        {
            new(string.Empty, noneLabel, string.Empty, noneLabel)
        };
        foreach (var def in MouseActions)
        {
            var label = AppUiLocalization.GetString(def.LabelResourceKey);
            var primary = string.Format(culture, optionFormat, def.ActionId, label);
            items.Add(new SelectionDialogItem(def.ActionId, primary, $"{def.Button}/{def.ActionMode}", $"{primary} {label}"));
        }

        return items;
    }
}
