using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class RadialMenuItemTests
{
    [Fact]
    public void LabelZhCnSetter_UpdatesLabelsDictionary()
    {
        var item = new RadialMenuItem();
        item.LabelZhCn = "  跳跃  ";
        Assert.NotNull(item.Labels);
        Assert.Equal("跳跃", item.Labels["zh-CN"]);
        item.LabelZhCn = string.Empty;
        Assert.Null(item.Labels);
    }

    [Fact]
    public void LabelEnUsSetter_UpdatesLabelsDictionary()
    {
        var item = new RadialMenuItem();
        item.LabelEnUs = "Jump";
        Assert.NotNull(item.Labels);
        Assert.Equal("Jump", item.Labels["en-US"]);
    }
}
