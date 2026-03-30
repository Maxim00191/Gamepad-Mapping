using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GamepadMapperGUI.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TriggerMoment
{
    Pressed,
    Released
}

public class MappingEntry
{
    [JsonProperty("from")]
    public GamepadBinding From { get; set; } = new();

    [JsonProperty("keyboardKey")]
    public string KeyboardKey { get; set; } = string.Empty;

    [JsonProperty("trigger")]
    public TriggerMoment Trigger { get; set; } = TriggerMoment.Pressed;

    // Optional: used when From.Type is LeftTrigger/RightTrigger etc.
    // When omitted, consumers can fall back to a sensible default.
    [JsonProperty("analogThreshold")]
    public float? AnalogThreshold { get; set; }
}

