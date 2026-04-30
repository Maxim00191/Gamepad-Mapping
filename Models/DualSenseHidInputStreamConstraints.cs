#nullable enable

namespace GamepadMapperGUI.Models;

public static class DualSenseHidInputStreamConstraints
{
    public const int ExpectedMaxReportRateHz = 250;
    public const int StaleReportDrainWindowMs = 1_000;
    public const int PrimaryReadTimeoutMs = 5;
    public const int DrainReadTimeoutMs = 1;
    public const int MaxDrainReadsPerPoll = ExpectedMaxReportRateHz * StaleReportDrainWindowMs / 1_000;
    public const long HealthLogIntervalMs = 30_000;
}
