#nullable enable

using System;

namespace GamepadMapperGUI.UploadTextPolicy;

/// <summary>
/// Invoked by <c>Resources/Embedded/gzip-policy.ps1</c> after building this assembly (optional).
/// Keeps encoding logic in one place with <see cref="UploadTextPolicyPayloadCodec"/>.
/// </summary>
public static class UploadTextPolicyOfflineEncoder
{
    public static byte[] EncodeGzipBytesWithSymmetricKey(ReadOnlySpan<byte> aes256Key, byte[] gzipPlaintext)
    {
        ArgumentNullException.ThrowIfNull(gzipPlaintext);
        return UploadTextPolicyPayloadCodec.EncryptGzipPayloadAesGcmV1(gzipPlaintext, aes256Key);
    }

    /// <summary>
    /// Decodes an envelope produced by <see cref="EncodeGzipBytesWithSymmetricKey"/> (tests and local verification).
    /// </summary>
    public static bool TryDecodeGzipBytesWithSymmetricKey(ReadOnlySpan<byte> aes256Key, byte[] envelope, out byte[]? gzipBytes)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return UploadTextPolicyPayloadCodec.TryDecryptGzipPayloadAesGcm(envelope, aes256Key, out gzipBytes);
    }
}
