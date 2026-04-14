using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Core;

namespace GamepadMapperGUI.Models;

public class GameProfileTemplate : IKeyboardActionCatalog
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("profileId")]
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Optional shared id for multiple profiles of the same game (file-name namespace when auto-creating ids; also used to relate profiles for target-process inheritance).
    /// When null, empty, or equal to <see cref="ProfileId"/>, this field is omitted in JSON and <see cref="EffectiveTemplateGroupId"/> is <see cref="ProfileId"/>.
    /// </summary>
    [JsonProperty("templateGroupId", NullValueHandling = NullValueHandling.Ignore)]
    public string? TemplateGroupId { get; set; }

    /// <summary>Logical game-group id: explicit <see cref="TemplateGroupId"/> when set, otherwise <see cref="ProfileId"/>.</summary>
    public string EffectiveTemplateGroupId
    {
        get
        {
            var g = (TemplateGroupId ?? string.Empty).Trim();
            return g.Length > 0 ? g : ProfileId;
        }
    }

    /// <summary>Optional single-level folder under the templates root (e.g. a game display name). Empty = templates root.</summary>
    [JsonProperty("templateCatalogFolder", NullValueHandling = NullValueHandling.Ignore)]
    public string? TemplateCatalogFolder { get; set; }

    // Backward compatibility for older templates that used "gameId".
    [JsonProperty("gameId")]
    private string LegacyGameId
    {
        set
        {
            if (string.IsNullOrWhiteSpace(TemplateGroupId) && !string.IsNullOrWhiteSpace(value))
                TemplateGroupId = value.Trim();
        }
    }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional template author (e.g. for a shared template catalog).</summary>
    [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
    public string? Author { get; set; }

    /// <summary>Short description for community template listings (plain text).</summary>
    [JsonProperty("communityListingDescription", NullValueHandling = NullValueHandling.Ignore)]
    public string? CommunityListingDescription { get; set; }

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

    /// <summary>Optional catalog of game actions (keyboard outputs). Mappings can use <see cref="MappingEntry.ActionId"/> instead of repeating <c>keyboardKey</c>.</summary>
    [JsonProperty("keyboardActions", NullValueHandling = NullValueHandling.Ignore)]
    public List<KeyboardActionDefinition>? KeyboardActions { get; set; }

    [JsonProperty("radialMenus", NullValueHandling = NullValueHandling.Ignore)]
    public List<RadialMenuDefinition>? RadialMenus { get; set; }

    [JsonProperty("mappings")]
    public List<MappingEntry> Mappings { get; set; } = new();

    public KeyboardActionDefinition? GetAction(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return null;
        return KeyboardActions?.FirstOrDefault(a => 
            string.Equals(a.Id, actionId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<KeyboardActionDefinition> GetAllActions()
    {
        return KeyboardActions ?? Enumerable.Empty<KeyboardActionDefinition>();
    }
}
