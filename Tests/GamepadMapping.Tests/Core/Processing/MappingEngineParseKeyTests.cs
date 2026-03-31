using System.Windows.Input;
using GamepadMapperGUI.Core;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing;

public class MappingEngineParseKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseKey_NullOrWhitespace_ReturnsNone(string? token)
    {
        Assert.Equal(Key.None, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("Space", Key.Space)]
    [InlineData("space", Key.Space)]
    [InlineData("Spacebar", Key.Space)]
    [InlineData("LeftCtrl", Key.LeftCtrl)]
    [InlineData("Ctrl", Key.LeftCtrl)]
    [InlineData("Esc", Key.Escape)]
    [InlineData("Return", Key.Enter)]
    [InlineData("0", Key.D0)]
    [InlineData("9", Key.D9)]
    public void ParseKey_CommonAliasesAndDigits_MapToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("Spacebar", "Space")]
    [InlineData("Ctrl", "LeftCtrl")]
    [InlineData("Esc", "Escape")]
    public void NormalizeKeyboardKeyToken_ReplacesAliases(string input, string expectedNormalized)
    {
        Assert.Equal(expectedNormalized, MappingEngine.NormalizeKeyboardKeyToken(input));
    }

    [Fact]
    public void ParseKey_UnknownToken_ReturnsNone()
    {
        Assert.Equal(Key.None, MappingEngine.ParseKey("NotARealKeyName_xyz"));
    }
}
