namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationSmokeRunResult
{
    public bool Ok { get; init; }

    public string? MessageResourceKey { get; init; }

    public string? Detail { get; init; }

    public IReadOnlyList<string> LogLines { get; init; } = [];
}
