using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class GameProfileTemplate
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    [JsonProperty("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("mappings")]
    public List<MappingEntry> Mappings { get; set; } = new();
}

