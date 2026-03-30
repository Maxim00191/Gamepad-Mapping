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
}

