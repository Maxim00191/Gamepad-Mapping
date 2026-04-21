using System.Collections.Generic;
using System.Globalization;
using System;
using System.Linq;
using Gamepad_Mapping.Models.State;
using Gamepad_Mapping.Utils;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class KeyboardActionSelectionBuilder : IKeyboardActionSelectionBuilder
{
    private const string ManualPickerLabelKey = "KeyboardActionPicker_ManualInputLabel";

    public IReadOnlyList<SelectionDialogItem> BuildSelectionItems(IEnumerable<KeyboardActionDefinition> keyboardActions)
    {
        var optionFormat = AppUiLocalization.GetString("KeyboardActionPicker_OptionFormat");
        var manual = AppUiLocalization.GetString(ManualPickerLabelKey);
        var culture = CultureInfo.CurrentUICulture;
        var items = new List<SelectionDialogItem>
        {
            new(string.Empty, manual, string.Empty, manual)
        };

        foreach (var action in keyboardActions)
        {
            var id = (action.Id ?? string.Empty).Trim();
            if (id.Length == 0)
                continue;
            var label = KeyboardActionPickerLabelConverter.FormatLabel(
                id,
                action.ResolvedCatalogDescription,
                optionFormat,
                manual,
                action.KeyboardKey,
                culture);
            var secondaryText = (action.KeyboardKey ?? string.Empty).Trim();
            items.Add(new SelectionDialogItem(id, label, secondaryText, $"{label} {id} {secondaryText}".Trim()));
        }

        return items;
    }

    public string BuildSelectedActionDisplayText(string? actionId, IEnumerable<KeyboardActionDefinition> keyboardActions)
    {
        var id = (actionId ?? string.Empty).Trim();
        if (id.Length == 0)
            return AppUiLocalization.GetString(ManualPickerLabelKey);

        var match = keyboardActions.FirstOrDefault(action =>
            string.Equals((action.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return id;

        return KeyboardActionPickerLabelConverter.FormatLabel(
            id,
            match.ResolvedCatalogDescription,
            AppUiLocalization.GetString("KeyboardActionPicker_OptionFormat"),
            AppUiLocalization.GetString(ManualPickerLabelKey),
            match.KeyboardKey,
            CultureInfo.CurrentUICulture);
    }
}
