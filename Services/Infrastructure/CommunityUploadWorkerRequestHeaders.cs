namespace GamepadMapperGUI.Services.Infrastructure;

internal static class CommunityUploadWorkerRequestHeaders
{
    internal const string CustomAuthKey = "X-Custom-Auth-Key";
    internal const string SignatureVersionKey = "X-Community-Signature-Version";
    internal const string SignatureVersionV1 = "v1";
    internal const string TimestampSecondsKey = "X-Community-Timestamp";
    internal const string NonceKey = "X-Community-Nonce";
    internal const string ContentSha256Key = "X-Community-Content-SHA256";
    internal const string SignatureKey = "X-Community-Signature";
    internal const string TicketIdKey = "X-Community-Ticket-Id";
    internal const string TicketProofKey = "X-Community-Ticket-Proof";
}
