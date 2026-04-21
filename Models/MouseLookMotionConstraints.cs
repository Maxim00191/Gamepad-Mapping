#nullable enable

namespace GamepadMapperGUI.Models;

/// <summary>
/// Tunables for mouse-look motion: subdivision of large relative moves and rebound timing vs gamepad poll rate.
/// Output is scheduled on the same cadence as <see cref="GamepadInputStreamConstraints"/> (default 10 ms ≈ 100 Hz sampling), not a separate mouse hardware report rate.
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
    /// Max synchronous <c>SendInput</c> / injection sub-moves per gamepad poll so bursts do not resemble a single-frame teleport.
    /// Remainder carries to the next poll on the same thread.
    /// </summary>
    public const int MaxSubMovesPerGamepadPoll = 12;
}
