namespace GamepadMapperGUI.Models;

public static class TouchpadGestureConstraints
{
    public const float MinSwipeDistanceNormalized = 0.08f;

    public const float MinDominantAxisRatio = 1.35f;

    public const float NormalizedDeltaToMouseScale = 520f;

    public const float FingerMotionIdleNormalized = 0.00005f;
}
