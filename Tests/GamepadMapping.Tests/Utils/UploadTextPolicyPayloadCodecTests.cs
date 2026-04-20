using System;
using System.Security.Cryptography;
using GamepadMapperGUI.UploadTextPolicy;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyPayloadCodecTests
{
    private const byte PayloadFormatVersionV1 = 1;
    private const byte PayloadFormatVersionV2 = 2;
    private const byte UnsupportedPayloadFormatVersion = 3;
    private const int NonceLengthBytes = 12;
    private const int TagLengthBytes = 16;
    private const int MinimumEnvelopeLengthBytes = 1 + NonceLengthBytes + TagLengthBytes;

    [Fact]
    public void AesGcmEnvelopeV2_RoundTrip_RestoresGzipPayload()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var envelope = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, original);
        Assert.NotNull(envelope);
        Assert.NotEmpty(envelope);
        Assert.Equal(PayloadFormatVersionV2, envelope[0]);

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

        var envelopeV1 = EncryptV1EnvelopeForCompatibilityTest(original, key);
        Assert.Equal(PayloadFormatVersionV1, envelopeV1[0]);

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
    public void TryDecodeGzipBytes_UnsupportedVersion_Fails()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var envelope = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, new byte[] { 1, 2, 3 });

        envelope[0] = UnsupportedPayloadFormatVersion;

        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelope, out var _));
    }

    [Fact]
    public void TryDecodeGzipBytes_ShorterThanMinimumEnvelopeLength_Fails()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var envelope = new byte[MinimumEnvelopeLengthBytes - 1];
        envelope[0] = PayloadFormatVersionV2;

        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelope, out var _));
    }

    [Fact]
    public void AesGcmEnvelopeV2_DowngradeToV1Marker_FailsAuthentication()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var envelopeV2 = UploadTextPolicyOfflineEncoder.EncodeGzipBytesWithSymmetricKey(key, new byte[] { 1, 3, 5, 7 });

        envelopeV2[0] = PayloadFormatVersionV1;

        Assert.False(UploadTextPolicyOfflineEncoder.TryDecodeGzipBytesWithSymmetricKey(key, envelopeV2, out var _));
    }

    private static byte[] EncryptV1EnvelopeForCompatibilityTest(ReadOnlySpan<byte> gzipPlaintext, ReadOnlySpan<byte> aes256Key)
    {
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
}
