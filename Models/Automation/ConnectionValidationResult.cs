namespace GamepadMapperGUI.Models.Automation;

public readonly record struct ConnectionValidationResult(
    bool IsAllowed,
    string? ReasonResourceKey,
    Guid? ExistingIncomingEdgeId = null);
