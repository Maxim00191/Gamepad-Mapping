using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core.Mapping;

public sealed class GamepadTouchpadFromValueCatalogTests
{
    [Theory]
    [InlineData("SWIPE_UP", TouchpadSwipeDirection.Up)]
    [InlineData("swipe_up", TouchpadSwipeDirection.Up)]
    [InlineData("SWIPELEFT", TouchpadSwipeDirection.Left)]
    public void TryParseSwipe_AcceptsSynonyms(string raw, TouchpadSwipeDirection expected)
    {
        Assert.True(GamepadTouchpadFromValueCatalog.TryParseSwipe(raw, out var dir));
        Assert.Equal(expected, dir);
    }

    [Fact]
    public void CanonicalizeForEditor_NormalizesSwipeTokens()
    {
        Assert.Equal("SWIPE_UP", GamepadTouchpadFromValueCatalog.CanonicalizeForEditor("swipe-up"));
    }
}
