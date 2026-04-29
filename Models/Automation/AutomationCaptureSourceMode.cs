using System;

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationCaptureSourceMode
{
    public const string Screen = "screen";
    public const string InProcessWindow = "process_window";

    // Backward-compatible alias for existing persisted values/usages.
    public const string ProcessWindow = InProcessWindow;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Screen;

        var trimmed = value.Trim();
        if (string.Equals(trimmed, InProcessWindow, StringComparison.OrdinalIgnoreCase))
            return InProcessWindow;
        if (string.Equals(trimmed, Screen, StringComparison.OrdinalIgnoreCase))
            return Screen;
        return Screen;
    }

    public static bool IsInProcessWindow(string? value) =>
        string.Equals(Normalize(value), InProcessWindow, StringComparison.OrdinalIgnoreCase);
}
