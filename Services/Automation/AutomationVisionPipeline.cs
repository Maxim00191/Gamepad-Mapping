#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationVisionPipeline : IAutomationVisionPipeline
{
    private readonly IReadOnlyDictionary<AutomationVisionAlgorithmKind, IAutomationVisionAlgorithm> _algorithms;
    private readonly SemaphoreSlim _workerGate;

    public AutomationVisionPipeline(IEnumerable<IAutomationVisionAlgorithm> algorithms, int maxParallelWorkers = 2)
    {
        _algorithms = algorithms.ToDictionary(a => a.Kind);
        _workerGate = new SemaphoreSlim(Math.Max(1, maxParallelWorkers));
    }

    public async ValueTask<AutomationVisionResult> ProcessAsync(
        AutomationVisionAlgorithmKind kind,
        AutomationVisionFrame frame,
        CancellationToken cancellationToken)
    {
        if (!_algorithms.TryGetValue(kind, out var algorithm))
            throw new InvalidOperationException($"Missing vision algorithm: {kind}");

        await _workerGate.WaitAsync(cancellationToken);
        try
        {
            return await algorithm.ProcessAsync(frame, cancellationToken);
        }
        finally
        {
            _workerGate.Release();
        }
    }
}
