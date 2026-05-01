#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapperGUI.Tests.Services;

public sealed class AutomationNodePropertyReaderTests
{
    [Fact]
    public void ReadStringList_ReturnsEmptyWhenMissing()
    {
        var props = new JsonObject();
        var list = AutomationNodePropertyReader.ReadStringList(props, AutomationNodePropertyKeys.FindImageAlternateNeedlePaths);
        Assert.Empty(list);
    }

    [Fact]
    public void ReadStringList_ParsesJsonStringArray()
    {
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlternateNeedlePaths] = new JsonArray("a/b.png", "c/d.png")
        };

        var list = AutomationNodePropertyReader.ReadStringList(props, AutomationNodePropertyKeys.FindImageAlternateNeedlePaths);

        Assert.Equal(["a/b.png", "c/d.png"], list);
    }

    [Fact]
    public void ReadStringList_ParsesSemicolonSeparatedLine()
    {
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlternateNeedlePaths] = " one.png ; two.png "
        };

        var list = AutomationNodePropertyReader.ReadStringList(props, AutomationNodePropertyKeys.FindImageAlternateNeedlePaths);

        Assert.Equal(["one.png", "two.png"], list);
    }

    [Fact]
    public void ReadStringList_ParsesNewlineSeparated()
    {
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlternateNeedlePaths] = "x/a.png\r\ny/b.png\n"
        };

        var list = AutomationNodePropertyReader.ReadStringList(props, AutomationNodePropertyKeys.FindImageAlternateNeedlePaths);

        Assert.Equal(["x/a.png", "y/b.png"], list);
    }

    [Fact]
    public void ReadBool_LegacyZeroCoordinatesWhenUnmatched_DefaultsFalse()
    {
        var props = new JsonObject();
        Assert.False(AutomationNodePropertyReader.ReadBool(props, AutomationNodePropertyKeys.FindImageLegacyZeroCoordinatesWhenUnmatched));
    }

    [Fact]
    public void ReadBool_LegacyZeroCoordinatesWhenUnmatched_ReadsTrue()
    {
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageLegacyZeroCoordinatesWhenUnmatched] = true
        };

        Assert.True(AutomationNodePropertyReader.ReadBool(props, AutomationNodePropertyKeys.FindImageLegacyZeroCoordinatesWhenUnmatched));
    }
}
