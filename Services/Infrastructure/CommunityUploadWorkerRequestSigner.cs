using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CommunityUploadWorkerRequestSigner : ICommunityUploadWorkerRequestSigner
{
    private readonly Func<DateTimeOffset> _utcNowFactory;
    private readonly Func<string> _nonceFactory;

    public CommunityUploadWorkerRequestSigner()
        : this(static () => DateTimeOffset.UtcNow, static () => Guid.NewGuid().ToString("N"))
    {
    }

    internal CommunityUploadWorkerRequestSigner(
        Func<DateTimeOffset> utcNowFactory,
        Func<string> nonceFactory)
    {
        _utcNowFactory = utcNowFactory;
        _nonceFactory = nonceFactory;
    }

    public void ApplySignatureHeaders(
        HttpRequestMessage request,
        Uri endpointUri,
        string requestBody,
        string signingKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(endpointUri);

        var timestampSeconds = _utcNowFactory().ToUnixTimeSeconds();
        var nonce = _nonceFactory();
        var contentHashHex = ComputeSha256Hex(requestBody ?? string.Empty);
        var canonicalPayload = string.Join(
            "\n",
            CommunityUploadWorkerRequestHeaders.SignatureVersionV1,
            request.Method.Method.ToUpperInvariant(),
            endpointUri.PathAndQuery,
            timestampSeconds.ToString(CultureInfo.InvariantCulture),
            nonce,
            contentHashHex);
        var signature = ComputeHmacBase64(signingKey ?? string.Empty, canonicalPayload);

        request.Headers.TryAddWithoutValidation(
            CommunityUploadWorkerRequestHeaders.SignatureVersionKey,
            CommunityUploadWorkerRequestHeaders.SignatureVersionV1);
        request.Headers.TryAddWithoutValidation(
            CommunityUploadWorkerRequestHeaders.TimestampSecondsKey,
            timestampSeconds.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(
            CommunityUploadWorkerRequestHeaders.NonceKey,
            nonce);
        request.Headers.TryAddWithoutValidation(
            CommunityUploadWorkerRequestHeaders.ContentSha256Key,
            contentHashHex);
        request.Headers.TryAddWithoutValidation(
            CommunityUploadWorkerRequestHeaders.SignatureKey,
            signature);
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeHmacBase64(string key, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(sigBytes);
    }
}
