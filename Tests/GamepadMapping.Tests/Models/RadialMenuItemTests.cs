using System.Collections.Generic;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class RadialMenuItemTests
{
    [Fact]
    public void Labels_Property_HoldsPerCultureSlotLabels()
    {
        var item = new RadialMenuItem
        {
            Labels = new Dictionary<string, string> { [TemplateLocaleKeys.ZhCn] = "跳跃" }
        };
        Assert.NotNull(item.Labels);
        Assert.Equal("跳跃", item.Labels[TemplateLocaleKeys.ZhCn]);
    }

    [Fact]
    public void Label_And_Labels_AreIndependentUntilLoadAppliesCulture()
    {
        var item = new RadialMenuItem
        {
            Label = "Jump",
            Labels = new Dictionary<string, string> { [TemplateLocaleKeys.ZhCn] = "跳跃" }
        };
        Assert.Equal("Jump", item.Label);
        Assert.Equal("跳跃", item.Labels![TemplateLocaleKeys.ZhCn]);
    }

    [Fact]
    public void LabelZhCn_Syncs_To_Labels_Dictionary()
    {
        var item = new RadialMenuItem();
        item.LabelZhCn = "  跳跃  ";
        Assert.NotNull(item.Labels);
        Assert.Equal("跳跃", item.Labels![TemplateLocaleKeys.ZhCn]);
        item.LabelZhCn = string.Empty;
        Assert.Null(item.Labels);
    }

    [Fact]
    public void LabelEnUs_Syncs_To_Labels_Dictionary()
    {
        var item = new RadialMenuItem();
        item.LabelEnUs = "Jump";
        Assert.NotNull(item.Labels);
        Assert.Equal("Jump", item.Labels![TemplateLocaleKeys.EnUs]);
    }
}
