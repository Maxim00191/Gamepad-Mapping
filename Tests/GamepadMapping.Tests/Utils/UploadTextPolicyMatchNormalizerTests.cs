#nullable enable

using System.Text;
using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyMatchNormalizerTests
{
    [Theory]
    [InlineData("b.a.d", "b.a.d")]
    [InlineData("b..a..d", "b..a..d")]
    [InlineData("b-a-d", "b-a-d")]
    [InlineData("b_a_d", "b_a_d")]
    [InlineData("b*a*d", "b*a*d")]
    [InlineData("f*ck", "f*ck")]
    [InlineData("fu(k", "fu(k")]
    public void NormalizeForPolicyMatch_PreservesInternalPunctuationAndSymbols(string input, string expected)
    {
        Assert.Equal(expected, UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(input));
    }

    [Fact]
    public void NormalizeForPolicyMatch_PreservesEmojiBetweenLetters()
    {
        Assert.Equal(
            "b\uD83D\uDE00a\uD83D\uDE00d",
            UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("b\uD83D\uDE00a\uD83D\uDE00d"));
    }

    [Fact]
    public void NormalizeForPolicyMatch_PreservesSpacesForWordBoundaries()
    {
        Assert.Equal("this bad here", UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("this bad here"));
    }

    [Theory]
    [InlineData("+q", "+q")]
    [InlineData("+Q", "+q")]
    [InlineData("+wechat", "+wechat")]
    [InlineData("$v", "$v")]
    [InlineData("q：", "q：")] // fullwidth colon — must not collapse to "q"
    [InlineData("q:", "q:")]
    public void NormalizeForPolicyMatch_PreservesContactStyleSymbols(string input, string expected)
    {
        Assert.Equal(expected, UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(input));
    }

    [Fact]
    public void IsWordContentRune_IsFalseForSpace()
    {
        Assert.False(UploadTextPolicyMatchNormalizer.IsWordContentRune(new Rune(' ')));
    }
}
