using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class AppSettings
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("templatesDirectory")]
    public string TemplatesDirectory { get; set; } = "Assets/Profiles/templates";

    [JsonProperty("defaultGameId")]
    public string DefaultGameId { get; set; } = "default";

    /// <summary>
    /// Shared timing (ms) for chord modifier grace, combo HUD reveal delay, and short-press vs hold-dual threshold. Clamped when applied.
    /// </summary>
    [JsonProperty("modifierGraceMs")]
    public int ModifierGraceMs { get; set; } = 500;
}
