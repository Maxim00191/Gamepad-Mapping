using System;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphSmokeRunner
{
    Task<AutomationSmokeRunResult> RunOnceAsync(
        AutomationGraphDocument document,
        CancellationToken cancellationToken = default,
        IProgress<string>? logLineProgress = null);
}
