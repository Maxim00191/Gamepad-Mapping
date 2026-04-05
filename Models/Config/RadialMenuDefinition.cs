using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class RadialMenuDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional resource key for the center title, resolved like <see cref="GameProfileTemplate.DisplayNameKey"/>.</summary>
    [JsonProperty("displayNameKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayNameKey { get; set; }

    /// <summary>Optional per-culture center titles (e.g. <c>zh-CN</c>), same pattern as <see cref="GameProfileTemplate.DisplayNames"/>.</summary>
    [JsonProperty("displayNames", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? DisplayNames { get; set; }

    /// <summary>
    /// Which joystick to use for selection: "LeftStick" or "RightStick"
    /// </summary>
    [JsonProperty("joystick")]
    public string Joystick { get; set; } = "RightStick";

    [JsonProperty("items")]
    public ObservableCollection<RadialMenuItem> Items { get; set; } = new();
}
