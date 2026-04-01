using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

/// <summary>
/// Switches the active profile to <see cref="AlternateProfileId"/> when the binding fires.
/// Use reciprocal bindings in each profile (A→B and B→A) so one button toggles control modes.
/// </summary>
public sealed class TemplateToggleBinding
{
    /// <summary>Template <c>profileId</c> to activate (filename stem under templates, case-insensitive).</summary>
    [JsonProperty("alternateProfileId")]
    public string AlternateProfileId { get; set; } = string.Empty;
}
