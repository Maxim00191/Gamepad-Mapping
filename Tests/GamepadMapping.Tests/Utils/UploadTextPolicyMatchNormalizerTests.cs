#nullable enable

using System.Text;
using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPolicyMatchNormalizerTests
{
    [Theory]
    [InlineData("b.a.d", "bad")]
    [InlineData("b..a..d", "bad")]
    [InlineData("b-a-d", "bad")]
    [InlineData("b_a_d", "bad")]
    [InlineData("b*a*d", "bad")]
    public void NormalizeForPolicyMatch_CollapsesSegmentationPunctuation(string input, string expected)
    {
        Assert.Equal(expected, UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch(input));
    }

    [Fact]
    public void NormalizeForPolicyMatch_RemovesEmojiBetweenLetters()
    {
        Assert.Equal("bad", UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("b\uD83D\uDE00a\uD83D\uDE00d"));
    }

    [Fact]
    public void NormalizeForPolicyMatch_PreservesSpacesForWordBoundaries()
    {
        Assert.Equal("this bad here", UploadTextPolicyMatchNormalizer.NormalizeForPolicyMatch("this bad here"));
    }

    [Fact]
    public void IsWordContentRune_IsFalseForSpace()
    {
        Assert.False(UploadTextPolicyMatchNormalizer.IsWordContentRune(new Rune(' ')));
    }
}
