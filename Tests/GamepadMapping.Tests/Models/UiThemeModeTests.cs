using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Models;

public class UiThemeModeTests
{
    [Theory]
    [InlineData(null, UiThemeMode.FollowSystem)]
    [InlineData("", UiThemeMode.FollowSystem)]
    [InlineData("  ", UiThemeMode.FollowSystem)]
    [InlineData("bogus", UiThemeMode.FollowSystem)]
    [InlineData("LIGHT", UiThemeMode.Light)]
    [InlineData("Dark", UiThemeMode.Dark)]
    [InlineData("followSystem", UiThemeMode.FollowSystem)]
    public void Normalize_MapsToCanonicalValues(string? input, string expected)
    {
        Assert.Equal(expected, UiThemeMode.Normalize(input));
    }

    [Fact]
    public void ResolveToLight_UsesCallbackForFollowSystem()
    {
        Assert.True(UiThemeMode.ResolveToLight(UiThemeMode.FollowSystem, () => true));
        Assert.False(UiThemeMode.ResolveToLight(UiThemeMode.FollowSystem, () => false));
    }

    [Fact]
    public void ResolveToLight_IgnoresCallbackForForcedThemes()
    {
        Assert.True(UiThemeMode.ResolveToLight(UiThemeMode.Light, () => false));
        Assert.False(UiThemeMode.ResolveToLight(UiThemeMode.Dark, () => true));
    }
}
