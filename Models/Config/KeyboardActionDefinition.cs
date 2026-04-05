using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

/// <summary>One game action in the profile's keyboard catalog; referenced by <see cref="MappingEntry.ActionId"/>.</summary>
public sealed class KeyboardActionDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("keyboardKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? KeyboardKey { get; set; }

    [JsonProperty("templateToggle", NullValueHandling = NullValueHandling.Ignore)]
    public TemplateToggleBinding? TemplateToggle { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("descriptionKey", NullValueHandling = NullValueHandling.Ignore)]
    public string? DescriptionKey { get; set; }

    [JsonProperty("descriptions", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? Descriptions { get; set; }
}
