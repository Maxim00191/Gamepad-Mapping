using System;
using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// Spreads sub-moves across a fraction of one gamepad poll interval. Each gap uses a fair share of the <em>remaining</em> wall-clock budget,
/// then <see cref="IHumanInputNoiseController.AdjustDelayMs"/> jitter is applied and clamped so the batch cannot exceed the budget
/// (avoiding multi-second gaps when noise amplitude scales delays).
/// </summary>
public sealed class HardwareStyledMouseSubMoveStepDelayer : IMouseSubMoveStepDelayer
{
    private readonly IHumanInputNoiseController _noise;
    private readonly Func<int> _getGamepadPollingIntervalMs;

    public HardwareStyledMouseSubMoveStepDelayer(
        IHumanInputNoiseController noise,
        Func<int> getGamepadPollingIntervalMs)
    {
        _noise = noise;
        _getGamepadPollingIntervalMs = getGamepadPollingIntervalMs;
    }

    public IMouseSubMoveScheduleSession BeginScheduleSession(int stepsInThisBatch, CancellationToken cancellationToken) =>
        new BudgetSession(
            _noise,
            MouseLookMotionConstraints.GetSubMoveScheduleBudgetMs(_getGamepadPollingIntervalMs()),
            Math.Max(1, stepsInThisBatch),
            cancellationToken);

    /// <summary>
    /// Computes one gap's delay and updates remaining budget/gap counts. Exposed for deterministic unit tests (no wall clock).
    /// </summary>
    internal static int ComputeNextGapDelayMs(
        ref int remainingBudgetMs,
        ref int gapsRemaining,
        IHumanInputNoiseController noise)
    {
        if (gapsRemaining <= 0)
            return 0;

        // Largest fair share of remaining time among gaps left (ceil division).
        int shareCeiling = (remainingBudgetMs + gapsRemaining - 1) / gapsRemaining;
        int nominal = Math.Max(MouseLookMotionConstraints.MinInterSubMoveBaseDelayMs, shareCeiling);
        nominal = Math.Min(nominal, remainingBudgetMs);

        int noisyMs = noise.AdjustDelayMs(nominal);
        int actualMs = Math.Clamp(noisyMs, 0, remainingBudgetMs);

        remainingBudgetMs -= actualMs;
        gapsRemaining--;
        return actualMs;
    }

    private sealed class BudgetSession : IMouseSubMoveScheduleSession
    {
        private readonly IHumanInputNoiseController _noise;
        private readonly CancellationToken _cancellationToken;
        private int _remainingBudgetMs;
        private int _gapsRemaining;

        public BudgetSession(IHumanInputNoiseController noise, int budgetMs, int stepsInThisBatch, CancellationToken cancellationToken)
        {
            _noise = noise;
            _cancellationToken = cancellationToken;
            _remainingBudgetMs = Math.Max(1, budgetMs);
            _gapsRemaining = Math.Max(0, stepsInThisBatch - 1);
        }

        public async ValueTask DelayBeforeNextSubMoveAsync(CancellationToken cancellationToken = default)
        {
            if (_gapsRemaining <= 0)
                return;

            CancellationToken ct = cancellationToken.CanBeCanceled ? cancellationToken : _cancellationToken;

            int actualMs = ComputeNextGapDelayMs(ref _remainingBudgetMs, ref _gapsRemaining, _noise);

            if (actualMs > 0)
                await PreciseDelay.DelayAsync(actualMs, ct).ConfigureAwait(false);
        }
    }
}
