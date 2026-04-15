#nullable enable

using System.Text.Json.Serialization;

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class CommunityTemplateWorkerTicketAck
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("ticketId")]
    public string? TicketId { get; init; }

    [JsonPropertyName("ticketProof")]
    public string? TicketProof { get; init; }

    [JsonPropertyName("expiresAtUnixSeconds")]
    public long? ExpiresAtUnixSeconds { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }
}
