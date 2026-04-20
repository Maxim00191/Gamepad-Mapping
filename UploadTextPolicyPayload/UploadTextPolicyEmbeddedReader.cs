#nullable enable

using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GamepadMapperGUI.UploadTextPolicy;

/// <summary>
/// Loads the AES-256-GCM–protected, gzip-compressed upload text policy embedded in this assembly.
/// The policy assembly may be obfuscated at publish time; callers should use only this public entry point.
/// </summary>
public static class UploadTextPolicyEmbeddedReader
{
    public static bool TryReadPlaintextPolicyUtf8(out string? plaintext)
    {
        plaintext = null;
        byte[]? symKey = null;
        byte[]? gzipPlaintext = null;
        try
        {
            var asm = typeof(UploadTextPolicyEmbeddedReader).Assembly;
            using var keyStream = asm.GetManifestResourceStream(UploadTextPolicyPayloadCodec.SymmetricKeyResourceName);
            using var payloadStream = asm.GetManifestResourceStream(UploadTextPolicyPayloadCodec.EncryptedPayloadResourceName);
            if (keyStream is null || payloadStream is null)
                return false;

            symKey = ReadAllBytes(keyStream);
            if (symKey.Length != UploadTextPolicyPayloadCodec.SymmetricKeyByteLength)
                return false;

            var envelope = ReadAllBytes(payloadStream);
            if (!UploadTextPolicyPayloadCodec.TryDecryptGzipPayloadAesGcm(envelope, symKey, out gzipPlaintext) || gzipPlaintext is null)
                return false;

            using var ms = new MemoryStream(gzipPlaintext, writable: false);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            plaintext = reader.ReadToEnd();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (symKey is not null)
                CryptographicOperations.ZeroMemory(symKey);
            if (gzipPlaintext is not null)
                CryptographicOperations.ZeroMemory(gzipPlaintext);
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
