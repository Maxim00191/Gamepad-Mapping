#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed record AutomationHumanNoiseTarget(
    AutomationHumanNoiseTargetKind Kind,
    Guid? NodeId = null)
{
    public static AutomationHumanNoiseTarget Unknown { get; } = new(AutomationHumanNoiseTargetKind.Unknown);
}
