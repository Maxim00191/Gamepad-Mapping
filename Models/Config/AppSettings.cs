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
    /// Shared timing (ms) for chord modifier grace and combo HUD reveal delay. Hold-bind duration uses each mapping's
    /// <c>holdThresholdMs</c> when set; otherwise this value is the hold threshold fallback. Clamped when applied.
    /// </summary>
    [JsonProperty("modifierGraceMs")]
    public int ModifierGraceMs { get; set; } = 500;

    /// <summary>
    /// Applies only to buttons listed as combo leads in the profile (<c>comboLeadButtons</c>) or inferred from mappings.
    /// If held longer than this (ms) and then released without a combo path, suppress the solo Released mapping and hold-dual
    /// short (tap) so cancelling a combo does not fire those keys. Clamped when applied.
    /// </summary>
    [JsonProperty("leadKeyReleaseSuppressMs")]
    public int LeadKeyReleaseSuppressMs { get; set; } = 500;
}
