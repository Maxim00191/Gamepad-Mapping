using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

/// <summary>
/// Switches the active profile to <see cref="AlternateProfileId"/> when the binding fires.
/// Use reciprocal bindings in each profile (A→B and B→A) so one button toggles control modes.
/// </summary>
public sealed class TemplateToggleBinding
{
    /// <summary>
    /// Profile to activate: may be the template file name without <c>.json</c>, or the same value as
    /// <see cref="GameProfileTemplate.ProfileId"/> inside that file when it differs from the file name (resolved after template list load).
    /// </summary>
    [JsonProperty("alternateProfileId")]
    public string AlternateProfileId { get; set; } = string.Empty;
}
