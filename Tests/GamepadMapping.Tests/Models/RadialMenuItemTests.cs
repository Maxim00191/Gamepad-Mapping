using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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
    public void LabelPrimary_And_LabelSecondary_RoundTrip_ForEnglishUi()
    {
        var prev = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var item = new RadialMenuItem();
            item.LabelPrimary = "Open map";
            item.LabelSecondary = "打开地图";
            Assert.Equal("Open map", item.Label.Trim());
            Assert.Equal("打开地图", item.Labels?[TemplateLocaleKeys.ZhCn]);
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = prev;
        }
    }

    [Fact]
    public void LabelPrimary_And_LabelSecondary_RoundTrip_ForChineseUi()
    {
        var prev = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var item = new RadialMenuItem();
            item.LabelPrimary = "打开地图";
            item.LabelSecondary = "Open map";
            Assert.Equal("Open map", item.Label.Trim());
            Assert.Equal("打开地图", item.Labels?[TemplateLocaleKeys.ZhCn]);
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = prev;
        }
    }
}
