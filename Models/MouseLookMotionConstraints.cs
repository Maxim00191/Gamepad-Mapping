#nullable enable

namespace GamepadMapperGUI.Models;

/// <summary>
/// Tunables for mouse-look motion: subdivision of large relative moves and rebound timing vs gamepad poll rate.
/// Gamepad sampling uses <see cref="GamepadInputStreamConstraints"/> (default 10 ms ≈ 100 Hz). Sub-moves may be emitted between polls using wall-clock spacing (see hardware-style <c>IMouseSubMoveStepDelayer</c>).
/// </summary>
public static class MouseLookMotionConstraints
{
    /// <summary>Window (ms) used to scale rebound-suppression frame count with gamepad poll interval.</summary>
    public const int ReboundSuppressionCalibrationWindowMs = 65;

    /// <summary>Below this Chebyshev span, emit one relative move (matches prior humanizing behavior).</summary>
    public const int MinPixelSpanToSubdivide = 4;

    /// <summary>Target max Chebyshev norm per sub-move when subdividing (smaller = smoother, more events).</summary>
    public const int MaxChebyshevPixelsPerSubMove = 2;

    /// <summary>Upper bound on sub-move count for a single logical delta (spread across polls when combined with <see cref="MaxSubMovesPerGamepadPoll"/>).</summary>
    public const int MaxSmoothSubSteps = 48;

    /// <summary>
    /// Max sub-moves taken from one logical thumbstick delta per gamepad poll (remaining interpolation is carried).
    /// With async hardware-style scheduling, each sub-move is spaced in wall-clock time rather than burst on one thread.
    /// </summary>
    public const int MaxSubMovesPerGamepadPoll = 12;

    /// <summary>
    /// Fraction of one gamepad poll interval used as the wall-clock budget to space sub-moves (see hardware-style delayer).
    /// </summary>
    public const float SubMoveScheduleFractionOfPollInterval = 0.85f;

    /// <summary>
    /// Milliseconds of wall-clock budget for spacing sub-moves in one batch, derived from a raw poll-interval setting
    /// (clamped the same way as the gamepad read loop).
    /// </summary>
    public static int GetSubMoveScheduleBudgetMs(int pollIntervalMs)
    {
        int pollMs = GamepadInputStreamConstraints.ClampPollingIntervalMs(pollIntervalMs);
        return Math.Max(
            1,
            (int)Math.Round(pollMs * (double)SubMoveScheduleFractionOfPollInterval));
    }

    /// <summary>
    /// Minimum base gap (ms) between sub-moves before human noise jitter; keeps spacing above zero when poll interval is short.
    /// </summary>
    public const int MinInterSubMoveBaseDelayMs = 1;

    /// <summary>
    /// Number of independent mouse-look carry/sub-move channels: unscoped, left thumbstick, right thumbstick.
    /// </summary>
    public const int SubMoveSubdivisionScopeCount = 3;

    /// <summary>
    /// Max time to wait for async sub-move workers to finish after cancellation when clearing pending subdivision.
    /// </summary>
    public const int AsyncSubMoveScopeIdleWaitTimeoutMs = 10_000;
}
