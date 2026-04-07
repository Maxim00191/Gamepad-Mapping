using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Models;

public class GameProfileTemplateTests
{
    [Theory]
    [InlineData(null, "p1", "p1")]
    [InlineData("", "p1", "p1")]
    [InlineData("  ", "p1", "p1")]
    [InlineData("g1", "p1", "g1")]
    public void EffectiveTemplateGroupId_UsesExplicitGroupOrProfileId(string? group, string profileId, string expected)
    {
        var t = new GameProfileTemplate { ProfileId = profileId, TemplateGroupId = group };
        Assert.Equal(expected, t.EffectiveTemplateGroupId);
    }
}
