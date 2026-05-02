#nullable enable

using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationInputModeCatalog
{
    public const string LegacyWinInjectionAlias = "WinInjection";

    public static IReadOnlyList<string> SelectableModeIds { get; } =
    [
        InputEmulationApiIds.Win32,
        InputEmulationApiIds.InputInjection
    ];

    public static string NormalizeModeId(string? requestedModeId)
    {
        var trimmed = (requestedModeId ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        if (string.Equals(trimmed, LegacyWinInjectionAlias, StringComparison.OrdinalIgnoreCase))
            return InputEmulationApiIds.InputInjection;
        if (string.Equals(trimmed, InputEmulationApiIds.Win32, StringComparison.OrdinalIgnoreCase))
            return InputEmulationApiIds.Win32;
        if (string.Equals(trimmed, InputEmulationApiIds.InputInjection, StringComparison.OrdinalIgnoreCase))
            return InputEmulationApiIds.InputInjection;

        return trimmed;
    }
}
