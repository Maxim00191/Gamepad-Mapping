using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

/// <summary>Versioned wrapper for in-app copy/paste of a single mapping or catalog row between templates.</summary>
public sealed class ProfileRuleClipboardEnvelope
{
    public const int CurrentVersion = 1;

    [JsonProperty("v")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Identifies payloads produced by this app (not for cross-app interchange).</summary>
    [JsonProperty("app")]
    public string AppId { get; set; } = "gamepad-mapper";

    [JsonProperty("kind")]
    public ProfileRuleClipboardKind Kind { get; set; }

    /// <summary>
    /// JSON for a single <see cref="MappingEntry"/>, <see cref="KeyboardActionDefinition"/>, or <see cref="RadialMenuDefinition"/>,
    /// or a JSON array of the same type when multiple rows were copied.
    /// </summary>
    [JsonProperty("payload")]
    public string PayloadJson { get; set; } = "{}";
}
