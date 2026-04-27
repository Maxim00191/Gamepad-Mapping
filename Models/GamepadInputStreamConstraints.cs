#nullable enable

namespace GamepadMapperGUI.Models;

/// <summary>
/// Shared bounds for gamepad read loop tuning (settings UI + <see cref="Core.GamepadReader"/> + analog mapping math).
/// </summary>
public static class GamepadInputStreamConstraints
{
    public const int MinPollingIntervalMs = 5;
    public const int MaxPollingIntervalMs = 30;

    public const float MinAnalogChangeEpsilon = 0.001f;
    public const float MaxAnalogChangeEpsilon = 0.1f;

    public static int ClampPollingIntervalMs(int ms) =>
        Math.Clamp(ms, MinPollingIntervalMs, MaxPollingIntervalMs);

    public static float ClampAnalogChangeEpsilon(float epsilon) =>
        Math.Clamp(epsilon, MinAnalogChangeEpsilon, MaxAnalogChangeEpsilon);
}
