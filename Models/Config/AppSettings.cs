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

    /// <summary>Profile id (<c>*.json</c> stem) of the template last chosen in the UI; restored on next launch.</summary>
    [JsonProperty("lastSelectedTemplateProfileId")]
    public string? LastSelectedTemplateProfileId { get; set; }

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

    /// <summary>
    /// Legacy shared thumbstick deadzone (normalized [0..1]). When per-stick values are unset, this acts as the default.
    /// </summary>
    [JsonProperty("thumbstickDeadzone")]
    public float ThumbstickDeadzone { get; set; } = 0.10f;

    /// <summary>Normalized [0..1] deadzone for the left stick; falls back to <see cref="ThumbstickDeadzone"/> when non-positive.</summary>
    [JsonProperty("leftThumbstickDeadzone")]
    public float LeftThumbstickDeadzone { get; set; }

    /// <summary>Normalized [0..1] deadzone for the right stick; falls back to <see cref="ThumbstickDeadzone"/> when non-positive.</summary>
    [JsonProperty("rightThumbstickDeadzone")]
    public float RightThumbstickDeadzone { get; set; }
}
