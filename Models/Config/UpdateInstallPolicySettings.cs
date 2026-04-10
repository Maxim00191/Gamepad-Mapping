using System.Collections.Generic;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class UpdateInstallPolicySettings
{
    /// <summary>
    /// Relative paths preserved during update install. Entries can be directories (prefix match)
    /// or a specific file path.
    /// </summary>
    [JsonProperty("preservePaths", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> PreservePaths { get; set; } =
    [
        "Assets/Profiles/templates",
        "Assets/Config/local_settings.json"
    ];

    /// <summary>
    /// When true, files that are not present in the update package are removed (except preserved/system paths).
    /// </summary>
    [JsonProperty("removeOrphanFiles")]
    public bool RemoveOrphanFiles { get; set; } = true;
}
