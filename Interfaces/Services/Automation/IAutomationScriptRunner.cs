using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationScriptRunner
{
    Task<AutomationSmokeRunResult> RunDocumentOnceAsync(
        AutomationGraphDocument document,
        CancellationToken cancellationToken = default);

    Task<AutomationSmokeRunResult> RunFileOnceAsync(string scriptPath, CancellationToken cancellationToken = default);
}
