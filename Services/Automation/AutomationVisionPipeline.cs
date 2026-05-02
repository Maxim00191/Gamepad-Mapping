#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationVisionPipeline : IAutomationVisionPipeline
{
    private readonly IReadOnlyDictionary<AutomationVisionAlgorithmKind, IAutomationVisionAlgorithm> _algorithms;

    public AutomationVisionPipeline(IEnumerable<IAutomationVisionAlgorithm> algorithms)
    {
        _algorithms = algorithms.ToDictionary(a => a.Kind);
    }

    public async ValueTask<AutomationVisionResult> ProcessAsync(
        AutomationVisionAlgorithmKind kind,
        AutomationVisionFrame frame,
        CancellationToken cancellationToken)
    {
        if (!_algorithms.TryGetValue(kind, out var algorithm))
            throw new InvalidOperationException($"Missing vision algorithm: {kind}");

        return await algorithm.ProcessAsync(frame, cancellationToken);
    }
}
