#nullable enable

using System.Globalization;
using System.Windows;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Automation;
using Gamepad_Mapping.Models.State;
using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationInputModeSelectionService : IAutomationInputModeSelectionService
{
    private const string InputModeDialogTitleKey = "AutomationInputModePicker_DialogTitle";
    private const string InputModeSearchPlaceholderKey = "AutomationInputModePicker_SearchPlaceholder";
    private const string InputModeGlobalOptionKey = "AutomationInputModePicker_Global";
    private const string Win32LabelKey = "InputApiWin32Label";
    private const string InputInjectionLabelKey = "InputApiInputInjectionLabel";

    private readonly IItemSelectionDialogService _itemSelectionDialogService;

    public AutomationInputModeSelectionService(IItemSelectionDialogService itemSelectionDialogService)
    {
        _itemSelectionDialogService = itemSelectionDialogService;
    }

    public string BuildInputModePickerDisplayText(string? modeId)
    {
        var normalized = AutomationInputModeCatalog.NormalizeModeId(modeId);
        if (normalized.Length == 0)
            return AppUiLocalization.GetString(InputModeGlobalOptionKey);

        var label = ResolveLabelResourceKey(normalized) is { } labelKey
            ? AppUiLocalization.GetString(labelKey)
            : normalized;
        return string.Format(
            CultureInfo.CurrentUICulture,
            GetOptionFormat(),
            normalized,
            label);
    }

    public string? PickInputModeId(Window? owner, string? initiallySelectedModeId)
    {
        var normalizedInitialSelection = AutomationInputModeCatalog.NormalizeModeId(initiallySelectedModeId);
        return _itemSelectionDialogService.Select(
            owner,
            AppUiLocalization.GetString(InputModeDialogTitleKey),
            AppUiLocalization.GetString(InputModeSearchPlaceholderKey),
            BuildItems(),
            normalizedInitialSelection);
    }

    private static string? ResolveLabelResourceKey(string modeId)
    {
        if (string.Equals(modeId, InputEmulationApiIds.Win32, StringComparison.Ordinal))
            return Win32LabelKey;
        if (string.Equals(modeId, InputEmulationApiIds.InputInjection, StringComparison.Ordinal))
            return InputInjectionLabelKey;

        return null;
    }

    private static IReadOnlyList<SelectionDialogItem> BuildItems()
    {
        var optionFormat = GetOptionFormat();
        var culture = CultureInfo.CurrentUICulture;
        var globalLabel = AppUiLocalization.GetString(InputModeGlobalOptionKey);
        var items = new List<SelectionDialogItem>
        {
            new(string.Empty, globalLabel, string.Empty, globalLabel)
        };

        foreach (var modeId in AutomationInputModeCatalog.SelectableModeIds)
        {
            var labelKey = ResolveLabelResourceKey(modeId);
            var label = labelKey is null ? modeId : AppUiLocalization.GetString(labelKey);
            var primary = string.Format(culture, optionFormat, modeId, label);
            items.Add(new SelectionDialogItem(modeId, primary, label, $"{primary} {label}"));
        }

        return items;
    }

    private static string GetOptionFormat()
    {
        var format = AppUiLocalization.GetString("KeyboardActionPicker_OptionFormat");
        return format.Contains("{0}", StringComparison.Ordinal) && format.Contains("{1}", StringComparison.Ordinal)
            ? format
            : "{0} - {1}";
    }
}
