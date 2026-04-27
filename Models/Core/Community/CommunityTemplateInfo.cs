#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace GamepadMapperGUI.Models;

public class CommunityTemplateInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional <c>displayNameKey</c> from the source template JSON for .resx lookup (same as <see cref="GameProfileTemplate.DisplayNameKey"/>).</summary>
    [JsonProperty("displayNameKey", NullValueHandling = NullValueHandling.Ignore)]
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>Optional per-culture titles from the published index (same keys as <see cref="GameProfileTemplate.DisplayNames"/>).</summary>
    [JsonProperty("displayNames", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? DisplayNames { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonProperty("catalogFolder")]
    public string? CatalogFolder { get; set; }

    [JsonProperty("fileName")]
    public string? FileName { get; set; }

    [JsonProperty("relativePath")]
    public string? RelativePath { get; set; }

    [JsonProperty("tags")]
    public List<string>? Tags { get; set; }
}
