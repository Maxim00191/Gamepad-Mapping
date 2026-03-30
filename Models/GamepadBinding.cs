using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using CommunityToolkit.Mvvm.ComponentModel;

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

public class GamepadBinding : ObservableObject
{
    private GamepadBindingType _type = GamepadBindingType.Button;

    [JsonProperty("type")]
    public GamepadBindingType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    // For Type=Button this should be the Vortice.XInput.GamepadButtons enum name (e.g. "A", "B", "DPadUp").
    // For analog/thumbstick types this value can be used as a free-form identifier for the target mapping logic.
    private string _value = "A";

    [JsonProperty("value")]
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

