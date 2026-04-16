#nullable enable

using GamepadMapperGUI.Utils.Text;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public sealed class UploadTextPunctuationCanonicalizerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData("a，b", "a,b")]
    [InlineData("a。b", "a.b")]
    [InlineData("a．b", "a.b")]
    [InlineData("a；b", "a;b")]
    [InlineData("，。；", ",.;")]
    public void Canonicalize_ReplacesExpected(string input, string expected)
    {
        Assert.Equal(expected, UploadTextPunctuationCanonicalizer.Canonicalize(input));
    }

    [Fact]
    public void Canonicalize_ReturnsSameReference_WhenNoReplacement()
    {
        const string s = "ascii-only";
        Assert.Same(s, UploadTextPunctuationCanonicalizer.Canonicalize(s));
    }
}
