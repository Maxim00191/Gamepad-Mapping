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

    [JsonProperty("displayNameKey")]
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>Optional per-culture display names (e.g. <c>"zh-CN"</c>). Overrides <see cref="DisplayName"/> and resource <see cref="DisplayNameKey"/> when the current UI culture matches.</summary>
    [JsonProperty("displayNames", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? DisplayNames { get; set; }

    /// <summary>
    /// Executable base name for foreground gating (same as <see cref="System.Diagnostics.Process.ProcessName"/>, usually without .exe).
    /// Must be set manually in the profile JSON or in the UI; no process list is offered to reduce risk of mis-targeting system or protected processes.
    /// </summary>
    [JsonProperty("targetProcessName", NullValueHandling = NullValueHandling.Ignore)]
    public string? TargetProcessName { get; set; }

    /// <summary>
    /// Gamepad buttons that act as combo modifiers / leads for deferred solo press, long-release suppress, hold-dual tap suppress,
    /// and combo HUD extension lines. Use XInput button names (e.g. LeftShoulder).
    /// Omit or null to infer from mappings (non–ABXY in any chord with two or more inputs). Use an empty array for no leads.
    /// </summary>
    [JsonProperty("comboLeadButtons", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? ComboLeadButtons { get; set; }

    [JsonProperty("mappings")]
    public List<MappingEntry> Mappings { get; set; } = new();
}
