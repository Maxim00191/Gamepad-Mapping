using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ProfileServiceShareTargetTests
{
    [Theory]
    [InlineData("roco-kingdom-world", "roco-kingdom-world-fight")]
    [InlineData("roco-kingdom-world-fight", "roco-kingdom-world")]
    [InlineData("mygame", "mygame.extra")]
    [InlineData("a", "a-b")]
    [InlineData("same", "same")]
    public void ProfilesLikelyShareGameExecutable_Positive(string prev, string next) =>
        Assert.True(ProfileService.ProfilesLikelyShareGameExecutable(prev, next));

    [Theory]
    [InlineData(null, "a")]
    [InlineData("a", null)]
    [InlineData("", "a")]
    [InlineData("elden", "roco-kingdom-world")]
    [InlineData("game", "gamepad")]
    [InlineData("ab", "a")]
    public void ProfilesLikelyShareGameExecutable_Negative(string? prev, string? next) =>
        Assert.False(ProfileService.ProfilesLikelyShareGameExecutable(prev, next));
}

