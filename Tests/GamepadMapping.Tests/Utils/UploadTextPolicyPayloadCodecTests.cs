using System;
using System.Security.Cryptography;
using GamepadMapperGUI.UploadTextPolicy;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyPayloadCodecTests
{
    [Fact]
    public void AesGcmEnvelopeV2_RoundTrip_RestoresGzipPayload()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var envelope = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, original);
        Assert.NotNull(envelope);
        Assert.NotEmpty(envelope);
        Assert.Equal(UploadTextPolicyPayloadCodec.PayloadFormatVersionV2, envelope[0]);

        Assert.True(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelope, out var roundTrip));
        Assert.NotNull(roundTrip);
        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public void AesGcmEnvelopeV1_RemainsBackwardCompatible()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var original = new byte[] { 10, 20, 30 };

        var envelopeV1 = UploadTextPolicyPayloadCodec.EncryptGzipPayloadAesGcmV1(original, key);
        Assert.Equal(UploadTextPolicyPayloadCodec.PayloadFormatVersionV1, envelopeV1[0]);

        Assert.True(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelopeV1, out var roundTrip));
        Assert.NotNull(roundTrip);
        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public void TryDecodeGzipBytes_WrongKey_Fails()
    {
        var keyA = new byte[32];
        var keyB = new byte[32];
        RandomNumberGenerator.Fill(keyA);
        RandomNumberGenerator.Fill(keyB);

        var envelope = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(keyA, new byte[] { 9 });
        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(keyB, envelope, out var _));
    }

    [Fact]
    public void TryDecodeGzipBytes_TruncatedEnvelope_Fails()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var envelope = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, new byte[] { 1, 2, 3 });
        var truncated = new byte[envelope.Length / 2];
        Array.Copy(envelope, truncated, truncated.Length);

        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, truncated, out var _));
    }

    [Fact]
    public void AesGcmEnvelopeV2_DowngradeToV1Marker_FailsAuthentication()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var envelopeV2 = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, new byte[] { 1, 3, 5, 7 });

        envelopeV2[0] = UploadTextPolicyPayloadCodec.PayloadFormatVersionV1;

        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelopeV2, out var _));
    }
}
