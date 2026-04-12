using GamepadMapperGUI.Core;
using Xunit;

namespace GamepadMapping.Tests.Core.Mapping;

public class GamepadThumbstickFromValueCatalogTests
{
    [Theory]
    [InlineData("right", "RIGHT")]
    [InlineData("UP", "UP")]
    [InlineData("X", "X")]
    public void CanonicalizeForEditor_NormalizesPickListCase(string raw, string expected) =>
        Assert.Equal(expected, GamepadThumbstickFromValueCatalog.CanonicalizeForEditor(raw));

    [Fact]
    public void CanonicalizeForEditor_PreservesRecognizedNonPickListToken() =>
        Assert.Equal("FORWARD", GamepadThumbstickFromValueCatalog.CanonicalizeForEditor("forward"));

    [Fact]
    public void IsRecognizedStickInput_AcceptsEngineAliases() =>
        Assert.True(GamepadThumbstickFromValueCatalog.IsRecognizedStickInput("posx"));
}
