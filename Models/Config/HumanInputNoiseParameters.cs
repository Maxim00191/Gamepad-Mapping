namespace GamepadMapperGUI.Models;

/// <summary>Snapshot of human-noise UI settings for emulation (immutable).</summary>
public readonly record struct HumanInputNoiseParameters(
    bool Enabled,
    float Amplitude,
    float Frequency,
    float Smoothness)
{
    public static HumanInputNoiseParameters From(AppSettings settings) => new(
        settings.HumanNoiseEnabled,
        settings.HumanNoiseAmplitude,
        settings.HumanNoiseFrequency,
        settings.HumanNoiseSmoothness);
}
