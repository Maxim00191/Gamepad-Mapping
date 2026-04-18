#nullable enable

using System;
using System.Security.Cryptography;

namespace GamepadMapperGUI.UploadTextPolicy;

internal static class UploadTextPolicyPayloadCodec
{
    internal const string EncryptedPayloadResourceName = "GamepadMapperGUI.UploadTextPolicy.payload";
    internal const string SymmetricKeyResourceName = "GamepadMapperGUI.UploadTextPolicy.symkey";

    internal const int SymmetricKeyByteLength = 32;
    internal const byte PayloadFormatVersionV1 = 1;

    internal const int NonceLengthBytes = 12;
    internal const int TagLengthBytes = 16;

    /// <summary>
    /// Encrypts gzip-compressed payload bytes using AES-256-GCM (authenticated encryption).
    /// </summary>
    internal static byte[] EncryptGzipPayloadAesGcmV1(ReadOnlySpan<byte> gzipPlaintext, ReadOnlySpan<byte> aes256Key)
    {
        if (aes256Key.Length != SymmetricKeyByteLength)
            throw new ArgumentException($"AES key must be {SymmetricKeyByteLength} bytes.", nameof(aes256Key));

        using var aes = new AesGcm(aes256Key, TagLengthBytes);

        Span<byte> nonce = stackalloc byte[NonceLengthBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[gzipPlaintext.Length];
        var tag = new byte[TagLengthBytes];
        aes.Encrypt(nonce, gzipPlaintext, ciphertext, tag);

        var envelope = new byte[1 + NonceLengthBytes + ciphertext.Length + tag.Length];
        envelope[0] = PayloadFormatVersionV1;
        nonce.CopyTo(envelope.AsSpan(1, NonceLengthBytes));
        Buffer.BlockCopy(ciphertext, 0, envelope, 1 + NonceLengthBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, envelope, 1 + NonceLengthBytes + ciphertext.Length, tag.Length);

        return envelope;
    }

    /// <summary>
    /// Decrypts and authenticates an envelope produced by <see cref="EncryptGzipPayloadAesGcmV1"/>.
    /// </summary>
    internal static bool TryDecryptGzipPayloadAesGcm(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> aes256Key, out byte[]? gzipPlaintext)
    {
        gzipPlaintext = null;
        if (aes256Key.Length != SymmetricKeyByteLength)
            return false;

        if (envelope.Length < 1 + NonceLengthBytes + TagLengthBytes)
            return false;

        if (envelope[0] != PayloadFormatVersionV1)
            return false;

        int cipherLen = envelope.Length - 1 - NonceLengthBytes - TagLengthBytes;
        if (cipherLen < 0)
            return false;

        ReadOnlySpan<byte> nonce = envelope.Slice(1, NonceLengthBytes);
        ReadOnlySpan<byte> ciphertext = envelope.Slice(1 + NonceLengthBytes, cipherLen);
        ReadOnlySpan<byte> tag = envelope.Slice(1 + NonceLengthBytes + cipherLen, TagLengthBytes);

        try
        {
            using var aes = new AesGcm(aes256Key, TagLengthBytes);
            var plain = new byte[cipherLen];
            aes.Decrypt(nonce, ciphertext, tag, plain);
            gzipPlaintext = plain;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
