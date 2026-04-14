#nullable enable

using System.Text.RegularExpressions;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerMappingOverlayLabelText
{
    private static readonly Regex WhitespaceRuns = new(@"\s+", RegexOptions.Singleline | RegexOptions.Compiled);

    public static string NormalizeForOverlay(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;
        return WhitespaceRuns.Replace(s.Trim(), " ");
    }
}
