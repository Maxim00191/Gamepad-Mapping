#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationCaptureApi
{
    public const string Gdi = "gdi";

    public const string DesktopDuplication = "desktop_duplication";

    public static string Normalize(string? captureApiId)
    {
        if (string.IsNullOrWhiteSpace(captureApiId))
            return Gdi;
        var t = captureApiId.Trim();
        if (t.Equals("dxgi", StringComparison.OrdinalIgnoreCase))
            return DesktopDuplication;
        return t;
    }
}
