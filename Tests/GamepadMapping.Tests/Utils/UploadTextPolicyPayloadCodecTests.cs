using System;
using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyPayloadCodecTests
{
    [Fact]
    public void ApplyXor_RoundTrip_RestoresOriginal()
    {
        var data = new byte[] { 1, 2, 3, 4, 5, 6 };
        var key = new byte[] { 0xAA, 0x55 };
        var original = (byte[])data.Clone();

        UploadTextPolicyPayloadCodec.ApplyXor(data, key);
        Assert.NotEqual(original, data);

        UploadTextPolicyPayloadCodec.ApplyXor(data, key);
        Assert.Equal(original, data);
    }

    [Fact]
    public void ApplyXor_EmptyKey_Throws()
    {
        var data = new byte[] { 1 };
        Assert.Throws<ArgumentException>(() => UploadTextPolicyPayloadCodec.ApplyXor(data, ReadOnlySpan<byte>.Empty));
    }
}
