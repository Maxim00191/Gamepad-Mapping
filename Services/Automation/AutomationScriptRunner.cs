#nullable enable

using System.IO;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScriptRunner : IAutomationScriptRunner
{
    private readonly IAutomationGraphSerializer _serializer;
    private readonly IAutomationGraphSmokeRunner _smokeRunner;

    public AutomationScriptRunner(
        IAutomationGraphSerializer serializer,
        IAutomationGraphSmokeRunner smokeRunner)
    {
        _serializer = serializer;
        _smokeRunner = smokeRunner;
    }

    public Task<AutomationSmokeRunResult> RunDocumentOnceAsync(
        AutomationGraphDocument document,
        CancellationToken cancellationToken = default,
        IProgress<string>? logLineProgress = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _smokeRunner.RunOnceAsync(document, cancellationToken, logLineProgress);
    }

    public async Task<AutomationSmokeRunResult> RunFileOnceAsync(string scriptPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            return new AutomationSmokeRunResult
            {
                Ok = false,
                MessageResourceKey = "AutomationSmoke_RunFailed",
                Detail = "automation_script_path_missing"
            };

        var normalizedPath = Path.GetFullPath(scriptPath.Trim());
        if (!File.Exists(normalizedPath))
            return new AutomationSmokeRunResult
            {
                Ok = false,
                MessageResourceKey = "AutomationSmoke_RunFailed",
                Detail = $"automation_script_not_found:{normalizedPath}"
            };

        var json = await File.ReadAllTextAsync(normalizedPath, cancellationToken);
        var document = _serializer.Deserialize(json);
        return await RunDocumentOnceAsync(document, cancellationToken);
    }
}
