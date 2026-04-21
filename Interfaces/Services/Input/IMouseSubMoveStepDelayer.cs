using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Factory for per-batch schedulers that insert wall-clock spacing between relative mouse sub-moves
/// produced by mouse-look subdivision, so multiple <c>SendInput</c> injections are not emitted back-to-back in a single tight loop.
/// </summary>
public interface IMouseSubMoveStepDelayer
{
    /// <summary>
    /// Begins a scheduling session for one batch of sub-moves (typically one gamepad poll worth, capped by <see cref="Models.MouseLookMotionConstraints.MaxSubMovesPerGamepadPoll"/>).
    /// </summary>
    /// <param name="stepsInThisBatch">Number of relative moves in this batch (≥ 1).</param>
    IMouseSubMoveScheduleSession BeginScheduleSession(int stepsInThisBatch, CancellationToken cancellationToken);
}

/// <summary>
/// Stateful spacing for a single batch: call <see cref="DelayBeforeNextSubMoveAsync"/> before each sub-move after the first.
/// </summary>
public interface IMouseSubMoveScheduleSession
{
    /// <summary>
    /// Waits before emitting the next sub-move. The first call for a batch should be made immediately before the <em>second</em> sub-move (no delay before the first).
    /// </summary>
    ValueTask DelayBeforeNextSubMoveAsync(CancellationToken cancellationToken = default);
}
