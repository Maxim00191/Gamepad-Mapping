#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace GamepadMapperGUI.UploadTextPolicy;

internal static class UploadTextPolicyPayloadCodec
{
    internal const string EncryptedPayloadResourceName = "GamepadMapperGUI.UploadTextPolicy.payload";
    internal const string SymmetricKeyResourceName = "GamepadMapperGUI.UploadTextPolicy.symkey";

    internal const int SymmetricKeyByteLength = 32;
    internal const byte PayloadFormatVersionV1 = 1;
    internal const byte PayloadFormatVersionV2 = 2;

    internal const int NonceLengthBytes = 12;
    internal const int TagLengthBytes = 16;

    private static readonly byte[] PayloadAadV2 = Encoding.UTF8.GetBytes("GM.UploadTextPolicy.Payload.v2");

    internal static byte[] EncryptGzipPayloadAesGcm(
        ReadOnlySpan<byte> gzipPlaintext,
        ReadOnlySpan<byte> aes256Key,
        byte payloadFormatVersion = PayloadFormatVersionV2)
    {
        return payloadFormatVersion switch
        {
            PayloadFormatVersionV1 => EncryptGzipPayloadAesGcmV1(gzipPlaintext, aes256Key),
            PayloadFormatVersionV2 => EncryptGzipPayloadAesGcmV2(gzipPlaintext, aes256Key),
            _ => throw new ArgumentOutOfRangeException(nameof(payloadFormatVersion), payloadFormatVersion, "Unsupported payload format version.")
        };
    }

    internal static byte[] EncryptGzipPayloadAesGcmV1(ReadOnlySpan<byte> gzipPlaintext, ReadOnlySpan<byte> aes256Key)
    {
        return EncryptGzipPayloadAesGcmCore(
            gzipPlaintext,
            aes256Key,
            PayloadFormatVersionV1,
            aad: null,
            deriveVersionScopedKey: false);
    }

    internal static byte[] EncryptGzipPayloadAesGcmV2(ReadOnlySpan<byte> gzipPlaintext, ReadOnlySpan<byte> aes256Key)
    {
        return EncryptGzipPayloadAesGcmCore(
            gzipPlaintext,
            aes256Key,
            PayloadFormatVersionV2,
            PayloadAadV2,
            deriveVersionScopedKey: true);
    }

    internal static bool TryDecryptGzipPayloadAesGcm(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> aes256Key, out byte[]? gzipPlaintext)
    {
        gzipPlaintext = null;
        if (aes256Key.Length != SymmetricKeyByteLength)
            return false;

        if (envelope.Length < 1 + NonceLengthBytes + TagLengthBytes)
            return false;

        var payloadVersion = envelope[0];
        if (payloadVersion != PayloadFormatVersionV1 && payloadVersion != PayloadFormatVersionV2)
            return false;

        int cipherLen = envelope.Length - 1 - NonceLengthBytes - TagLengthBytes;
        if (cipherLen < 0)
            return false;

        ReadOnlySpan<byte> nonce = envelope.Slice(1, NonceLengthBytes);
        ReadOnlySpan<byte> ciphertext = envelope.Slice(1 + NonceLengthBytes, cipherLen);
        ReadOnlySpan<byte> tag = envelope.Slice(1 + NonceLengthBytes + cipherLen, TagLengthBytes);

        byte[]? derivedKey = null;
        try
        {
            ReadOnlySpan<byte> keyToUse = aes256Key;
            ReadOnlySpan<byte> aad = default;
            if (payloadVersion == PayloadFormatVersionV2)
            {
                derivedKey = UploadTextPolicyKeyDerivation.DerivePayloadKey(aes256Key, payloadVersion);
                keyToUse = derivedKey;
                aad = PayloadAadV2;
            }

            using var aes = new AesGcm(keyToUse, TagLengthBytes);
            var plain = new byte[cipherLen];
            aes.Decrypt(nonce, ciphertext, tag, plain, aad);
            gzipPlaintext = plain;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            if (derivedKey is not null)
                CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static byte[] EncryptGzipPayloadAesGcmCore(
        ReadOnlySpan<byte> gzipPlaintext,
        ReadOnlySpan<byte> aes256Key,
        byte payloadVersion,
        byte[]? aad,
        bool deriveVersionScopedKey)
    {
        if (aes256Key.Length != SymmetricKeyByteLength)
            throw new ArgumentException($"AES key must be {SymmetricKeyByteLength} bytes.", nameof(aes256Key));

        byte[]? derivedKey = null;
        try
        {
            ReadOnlySpan<byte> keyToUse = aes256Key;
            if (deriveVersionScopedKey)
            {
                derivedKey = UploadTextPolicyKeyDerivation.DerivePayloadKey(aes256Key, payloadVersion);
                keyToUse = derivedKey;
            }

            using var aes = new AesGcm(keyToUse, TagLengthBytes);
            Span<byte> nonce = stackalloc byte[NonceLengthBytes];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[gzipPlaintext.Length];
            var tag = new byte[TagLengthBytes];
            aes.Encrypt(nonce, gzipPlaintext, ciphertext, tag, aad);

            var envelope = new byte[1 + NonceLengthBytes + ciphertext.Length + tag.Length];
            envelope[0] = payloadVersion;
            nonce.CopyTo(envelope.AsSpan(1, NonceLengthBytes));
            Buffer.BlockCopy(ciphertext, 0, envelope, 1 + NonceLengthBytes, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, envelope, 1 + NonceLengthBytes + ciphertext.Length, tag.Length);
            return envelope;
        }
        finally
        {
            if (derivedKey is not null)
                CryptographicOperations.ZeroMemory(derivedKey);
        }
    }
}
