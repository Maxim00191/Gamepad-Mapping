#nullable enable

using System.Globalization;
using System.Windows;
using Gamepad_Mapping.Models.State;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationVisionAlgorithmSelectionService : IAutomationVisionAlgorithmSelectionService
{
    private const string DialogTitleKey = "AutomationVisionAlgorithmPicker_DialogTitle";
    private const string SearchPlaceholderKey = "AutomationVisionAlgorithmPicker_SearchPlaceholder";

    private readonly IItemSelectionDialogService _itemSelectionDialogService;

    public AutomationVisionAlgorithmSelectionService(IItemSelectionDialogService itemSelectionDialogService)
    {
        _itemSelectionDialogService = itemSelectionDialogService;
    }

    public string BuildFindImageAlgorithmPickerDisplayText(string? algorithmId)
    {
        var normalized = NormalizeFindImageAlgorithmId(algorithmId);
        var option = AutomationVisionAlgorithmCatalog.FindImageAlgorithmChoiceOptions()
            .FirstOrDefault(x => string.Equals(x.StoredValue, normalized, StringComparison.Ordinal));
        return option is null
            ? normalized
            : AppUiLocalization.GetString(option.LabelResourceKey);
    }

    public string? PickFindImageAlgorithm(Window? owner, string? initiallySelectedAlgorithmId)
    {
        var normalizedInitialSelection = NormalizeFindImageAlgorithmId(initiallySelectedAlgorithmId);
        return _itemSelectionDialogService.Select(
            owner,
            AppUiLocalization.GetString(DialogTitleKey),
            AppUiLocalization.GetString(SearchPlaceholderKey),
            BuildItems(),
            normalizedInitialSelection);
    }

    private static string NormalizeFindImageAlgorithmId(string? algorithmId)
    {
        var kind = AutomationVisionAlgorithmStorage.ParseFindImageAlgorithmKind(algorithmId);
        return AutomationVisionAlgorithmStorage.ToStorageValue(kind);
    }

    private static IReadOnlyList<SelectionDialogItem> BuildItems()
    {
        var optionFormat = GetOptionFormat();
        var culture = CultureInfo.CurrentUICulture;
        var items = new List<SelectionDialogItem>();

        foreach (var option in AutomationVisionAlgorithmCatalog.FindImageAlgorithmChoiceOptions())
        {
            var label = AppUiLocalization.GetString(option.LabelResourceKey);
            var primary = string.Format(culture, optionFormat, option.StoredValue, label);
            items.Add(new SelectionDialogItem(option.StoredValue, primary, label, $"{primary} {label}"));
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
