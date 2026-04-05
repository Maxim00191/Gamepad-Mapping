using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class RadialMenuDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Which joystick to use for selection: "LeftStick" or "RightStick"
    /// </summary>
    [JsonProperty("joystick")]
    public string Joystick { get; set; } = "RightStick";

    [JsonProperty("items")]
    public List<RadialMenuItem> Items { get; set; } = new();
}

public class RadialMenuItem
{
    /// <summary>
    /// Reference to an Id in keyboardActions
    /// </summary>
    [JsonProperty("actionId")]
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon path or key
    /// </summary>
    [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
    public string? Icon { get; set; }
}
