#nullable enable

using System;

namespace GamepadMapperGUI.Utils.Text;

internal static class UploadTextPolicyPayloadCodec
{
    internal const string ObfuscatedGzipResourceName = "GamepadMapperGUI.UploadTextPolicy.txt.gz";
    internal const string XorKeyResourceName = "GamepadMapperGUI.UploadTextPolicy.xorkey";

    internal static void ApplyXor(Span<byte> data, ReadOnlySpan<byte> xorKey)
    {
        if (xorKey.Length == 0)
            throw new ArgumentException("Xor key must be non-empty.", nameof(xorKey));

        for (var i = 0; i < data.Length; i++)
            data[i] ^= xorKey[i % xorKey.Length];
    }
}
