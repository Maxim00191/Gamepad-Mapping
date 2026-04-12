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
    [InlineData("   ", "Space")]
    public void NormalizeKeyboardKeyToken_ReplacesAliases(string input, string expectedNormalized)
    {
        Assert.Equal(expectedNormalized, MappingEngine.NormalizeKeyboardKeyToken(input));
    }

    [Fact]
    public void ParseKey_UnknownToken_ReturnsNone()
    {
        Assert.Equal(Key.None, MappingEngine.ParseKey("NotARealKeyName_xyz"));
    }

    [Theory]
    [InlineData("OemOpenBrackets", Key.OemOpenBrackets)]
    [InlineData("OemQuotes", Key.OemQuotes)]
    [InlineData("OemPeriod", Key.OemPeriod)]
    [InlineData("OemComma", Key.OemComma)]
    [InlineData("OemMinus", Key.OemMinus)]
    [InlineData("OemPlus", Key.OemPlus)]
    [InlineData("OemCloseBrackets", Key.OemCloseBrackets)]
    [InlineData("OemBackslash", Key.OemBackslash)]
    [InlineData("OemSemicolon", Key.OemSemicolon)]
    [InlineData("OemQuestion", Key.OemQuestion)]
    [InlineData("OemTilde", Key.OemTilde)]
    public void ParseKey_SymbolKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("F1", Key.F1)]
    [InlineData("F2", Key.F2)]
    [InlineData("F5", Key.F5)]
    [InlineData("F12", Key.F12)]
    public void ParseKey_FunctionKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("PrintScreen", Key.PrintScreen)]
    [InlineData("Scroll", Key.Scroll)]
    [InlineData("Pause", Key.Pause)]
    [InlineData("Insert", Key.Insert)]
    [InlineData("Delete", Key.Delete)]
    [InlineData("Home", Key.Home)]
    [InlineData("End", Key.End)]
    [InlineData("PageUp", Key.PageUp)]
    [InlineData("PageDown", Key.PageDown)]
    public void ParseKey_SystemKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("Left", Key.Left)]
    [InlineData("Right", Key.Right)]
    [InlineData("Up", Key.Up)]
    [InlineData("Down", Key.Down)]
    public void ParseKey_ArrowKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("NumPad0", Key.NumPad0)]
    [InlineData("NumPad9", Key.NumPad9)]
    [InlineData("Divide", Key.Divide)]
    [InlineData("Multiply", Key.Multiply)]
    [InlineData("Subtract", Key.Subtract)]
    [InlineData("Add", Key.Add)]
    [InlineData("Decimal", Key.Decimal)]
    public void ParseKey_NumpadKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("oemopenBrackets", Key.OemOpenBrackets)]
    [InlineData("f12", Key.F12)]
    [InlineData("PRINTSCREEN", Key.PrintScreen)]
    [InlineData("numpad5", Key.NumPad5)]
    public void ParseKey_SpecialKeysWithVariedCase_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("LeftShift", Key.LeftShift)]
    [InlineData("RightShift", Key.RightShift)]
    [InlineData("LeftAlt", Key.LeftAlt)]
    [InlineData("RightAlt", Key.RightAlt)]
    [InlineData("LeftCtrl", Key.LeftCtrl)]
    [InlineData("RightCtrl", Key.RightCtrl)]
    public void ParseKey_ModifierKeysVariants_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("Tab", Key.Tab)]
    [InlineData("CapsLock", Key.Capital)]
    [InlineData("NumLock", Key.NumLock)]
    public void ParseKey_OtherCommonKeys_MapsToExpectedKey(string token, Key expected)
    {
        Assert.Equal(expected, MappingEngine.ParseKey(token));
    }

    [Theory]
    [InlineData("OemOpenBrackets")]
    [InlineData("OemQuotes")]
    [InlineData("F12")]
    [InlineData("PrintScreen")]
    [InlineData("NumPad5")]
    public void ParseKey_SpecialKeys_NotNone(string token)
    {
        var result = MappingEngine.ParseKey(token);
        Assert.NotEqual(Key.None, result);
    }
}
