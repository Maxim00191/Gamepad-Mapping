#nullable enable

using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadLinkTextNormalizerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("plain", "plain")]
    [InlineData("ＨＴＴＰＳ：／／ＷＷＷ．ＥＸＡＭＰＬＥ．ＣＯＭ", "https://www.example.com")]
    [InlineData("h\u00E8tps://ex\u00E1mple.com", "hetps://example.com")]
    [InlineData("Ａ\u0301", "a")]
    public void NormalizeForLinkDetection_NormalizesExpected(string? input, string expected)
    {
        Assert.Equal(expected, UploadLinkTextNormalizer.NormalizeForLinkDetection(input));
    }
}
