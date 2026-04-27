using System.Windows.Input;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationKeyboardKeyParserTests
{
    [Theory]
    [InlineData("A", Key.A)]
    [InlineData("z", Key.Z)]
    [InlineData("Enter", Key.Enter)]
    public void TryParse_ValidKeys_ReturnsParsedValue(string input, Key expected)
    {
        var ok = AutomationKeyboardKeyParser.TryParse(input, out var parsed);

        Assert.True(ok);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Ctrl+X")]
    [InlineData("?")]
    public void TryParse_InvalidKeys_ReturnsFalse(string input)
    {
        var ok = AutomationKeyboardKeyParser.TryParse(input, out _);

        Assert.False(ok);
    }
}
