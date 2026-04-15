#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerGithubApiErrorPayload
{
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("errors")]
    public JsonElement Errors { get; init; }
}
