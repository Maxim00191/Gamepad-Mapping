using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GamepadMapperGUI.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TriggerMoment
{
    Pressed,
    Released,
    Tap
}

public class MappingEntry : ObservableObject
{
    private GamepadBinding _from = new();

    [JsonProperty("from")]
    public GamepadBinding From
    {
        get => _from;
        set => SetProperty(ref _from, value);
    }

    private string _keyboardKey = string.Empty;

    [JsonProperty("keyboardKey")]
    public string KeyboardKey
    {
        get => _keyboardKey;
        set => SetProperty(ref _keyboardKey, value);
    }

    private string _description = string.Empty;

    [JsonProperty("description")]
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string? _descriptionKey;

    [JsonProperty("descriptionKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? DescriptionKey
    {
        get => _descriptionKey;
        set => SetProperty(ref _descriptionKey, value);
    }

    /// <summary>Optional per-culture descriptions (e.g. <c>"zh-CN"</c>). Overrides <see cref="Description"/> and resource <see cref="DescriptionKey"/> when the current UI culture matches.</summary>
    [JsonProperty("descriptions", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Descriptions { get; set; }

    private TriggerMoment _trigger = TriggerMoment.Pressed;

    [JsonProperty("trigger")]
    public TriggerMoment Trigger
    {
        get => _trigger;
        set => SetProperty(ref _trigger, value);
    }

    // Optional: used when From.Type is LeftTrigger/RightTrigger etc.
    // When omitted, consumers can fall back to a sensible default.
    private float? _analogThreshold;

    [JsonProperty("analogThreshold")]
    public float? AnalogThreshold
    {
        get => _analogThreshold;
        set => SetProperty(ref _analogThreshold, value);
    }

    /// <summary>
    /// When set with <see cref="HoldThresholdMs"/> and <see cref="Trigger"/> is <see cref="TriggerMoment.Tap"/>,
    /// a short press/release before the threshold sends <see cref="KeyboardKey"/> once; holding at least that long
    /// sends <see cref="HoldKeyboardKey"/> once (button-only chords; no LT/RT modifiers in the chord).
    /// </summary>
    private string _holdKeyboardKey = string.Empty;

    [JsonProperty("holdKeyboardKey")]
    public string HoldKeyboardKey
    {
        get => _holdKeyboardKey;
        set => SetProperty(ref _holdKeyboardKey, value);
    }

    /// <summary>Minimum hold duration in milliseconds for the hold output (typical range 250–800).</summary>
    private int? _holdThresholdMs;

    [JsonProperty("holdThresholdMs")]
    public int? HoldThresholdMs
    {
        get => _holdThresholdMs;
        set => SetProperty(ref _holdThresholdMs, value);
    }
}
