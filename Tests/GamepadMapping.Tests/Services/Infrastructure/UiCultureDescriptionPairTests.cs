using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapping.Tests.Services.Infrastructure;

public class UiCultureDescriptionPairTests
{
    [Fact]
    public void WritePair_ZhUi_PersistsZhAndEnAndBaseline()
    {
        var map = (Dictionary<string, string>?)null;
        var baseline = string.Empty;
        var zhUi = CultureInfo.GetCultureInfo("zh-CN");
        UiCultureDescriptionPair.WritePair(ref map, ref baseline, zhUi, "跳跃", "Jump");
        Assert.NotNull(map);
        Assert.Equal("跳跃", map![TemplateLocaleKeys.ZhCn]);
        Assert.Equal("Jump", baseline);
    }

    [Fact]
    public void ReadPrimary_FallsBackToBaseline_WhenMapMissingLocale()
    {
        var map = new Dictionary<string, string>();
        var baseline = "Fallback";
        var zhUi = CultureInfo.GetCultureInfo("zh-CN");
        Assert.Equal("Fallback", UiCultureDescriptionPair.ReadPrimary(map, baseline, zhUi));
    }

    [Fact]
    public void ReadSecondary_ChineseUi_uses_baseline_when_enUs_missing_from_map()
    {
        var map = new Dictionary<string, string> { [TemplateLocaleKeys.ZhCn] = "跳跃" };
        var zhUi = CultureInfo.GetCultureInfo("zh-CN");
        Assert.Equal("Jump", UiCultureDescriptionPair.ReadSecondary(map, "Jump", zhUi));
    }
}
