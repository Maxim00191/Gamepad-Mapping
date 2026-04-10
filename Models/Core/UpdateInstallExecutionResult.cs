namespace GamepadMapperGUI.Models.Core;

public sealed record UpdateInstallExecutionResult(
    bool Succeeded,
    string? ErrorMessage = null);
