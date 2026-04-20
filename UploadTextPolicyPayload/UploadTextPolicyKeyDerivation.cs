#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace GamepadMapperGUI.UploadTextPolicy;

internal static class UploadTextPolicyKeyDerivation
{
    private static readonly byte[] DerivationContext = Encoding.UTF8.GetBytes(
        "GamepadMapperGUI.UploadTextPolicy::KeyDerivation::AES-GCM");

    internal static byte[] DerivePayloadKey(ReadOnlySpan<byte> aes256Key, byte payloadVersion)
    {
        if (aes256Key.Length != UploadTextPolicyPayloadCodec.SymmetricKeyByteLength)
            throw new ArgumentException(
                $"AES key must be {UploadTextPolicyPayloadCodec.SymmetricKeyByteLength} bytes.",
                nameof(aes256Key));

        var material = new byte[aes256Key.Length + DerivationContext.Length + 1];
        aes256Key.CopyTo(material);
        DerivationContext.CopyTo(material.AsSpan(aes256Key.Length));
        material[^1] = payloadVersion;

        try
        {
            return SHA256.HashData(material);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }
}
