#nullable enable

using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyInterLetterNormalizerTests
{
    [Theory]
    [InlineData("f.u.c.k", "fuck")]
    [InlineData("f/u/c/k", "fuck")]
    [InlineData("f...u.c..k", "fuck")]
    [InlineData("b.a.d.p.h.r.a.s.e", "badphrase")]
    public void CollapseInterLetterObfuscation_CollapsesObfuscatedTokens(string input, string expected)
    {
        var n = UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(input);
        Assert.Equal(expected, UploadTextPolicyInterLetterNormalizer.CollapseInterLetterObfuscation(n));
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("e.g.")]
    [InlineData("x.y")]
    public void CollapseInterLetterObfuscation_PreservesSingleDotBetweenLettersWhenNotObfuscated(string token)
    {
        var n = UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(token);
        Assert.Equal(n, UploadTextPolicyInterLetterNormalizer.CollapseInterLetterObfuscation(n));
    }

    [Fact]
    public void ShouldDiscardPatternDueToInterLetterAsterisk_IsTrueForMaskedProfanityShape()
    {
        var n = UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("f*ck");
        Assert.True(UploadTextPolicyInterLetterNormalizer.ShouldDiscardPatternDueToInterLetterAsterisk(n));
    }

    [Fact]
    public void ShouldDiscardPatternDueToInterLetterAsterisk_IsFalseForContactStylePlusPrefix()
    {
        var n = UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("+wechat");
        Assert.False(UploadTextPolicyInterLetterNormalizer.ShouldDiscardPatternDueToInterLetterAsterisk(n));
    }
}
