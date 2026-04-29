#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationProcessWindowTarget
{
    public AutomationProcessWindowTarget(string? processName, int processId)
    {
        ProcessName = string.IsNullOrWhiteSpace(processName) ? string.Empty : NormalizeProcessName(processName);
        ProcessId = processId > 0 ? processId : 0;
    }

    public string ProcessName { get; }

    public int ProcessId { get; }

    public bool IsEmpty => ProcessId <= 0 && string.IsNullOrWhiteSpace(ProcessName);

    public string DisplayName => ProcessId > 0 && !string.IsNullOrWhiteSpace(ProcessName)
        ? $"{ProcessName} ({ProcessId})"
        : ProcessId > 0
            ? ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : ProcessName;

    public static AutomationProcessWindowTarget From(string? processName, int processId = 0) =>
        new(processName, processId);

    private static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^4].Trim();

        return trimmed;
    }
}
