#nullable enable

using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerTicketRequest
{
    [JsonPropertyName("payloadSha256")]
    public string PayloadSha256 { get; init; } = string.Empty;

    [JsonPropertyName("submitPath")]
    public string SubmitPath { get; init; } = string.Empty;

    [JsonPropertyName("turnstileToken")]
    public string? TurnstileToken { get; init; }
}
