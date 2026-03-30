using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GamepadMapperGUI.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum GamepadBindingType
{
    Button,
    LeftTrigger,
    RightTrigger,
    LeftThumbstick,
    RightThumbstick
}

public class GamepadBinding
{
    [JsonProperty("type")]
    public GamepadBindingType Type { get; set; } = GamepadBindingType.Button;

    // For Type=Button this should be the Vortice.XInput.GamepadButtons enum name (e.g. "A", "B", "DPadUp").
    // For analog/thumbstick types this value can be used as a free-form identifier for the target mapping logic.
    [JsonProperty("value")]
    public string Value { get; set; } = "A";
}

