#nullable enable

using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Utils.Text;

/// <summary>
/// Prepares raw field text for display in upload compliance issues (length-capped for UI safety).
/// </summary>
public static class UploadComplianceViolatingFieldText
{
    public static string PrepareForDisplay(string? raw)
    {
        var s = raw ?? string.Empty;
        var max = CommunityTemplateUploadConstraints.MaxComplianceIssueFieldDisplayCharacters;
        if (s.Length <= max)
            return s;

        return string.Concat(s.AsSpan(0, max).ToString(), "\u2026");
    }
}
