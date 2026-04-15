#nullable enable

using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerSubmissionFile
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; init; } = string.Empty;

    [JsonPropertyName("contentBase64")]
    public string ContentBase64 { get; init; } = string.Empty;
}
