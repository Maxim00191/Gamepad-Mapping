using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class CommunityTemplateDisplayLabelsTests
{
    [Fact]
    public void ResolveDisplayName_index_uses_zhCn_map_when_ui_is_zhCn()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("zh-CN") };
        var info = new CommunityTemplateInfo
        {
            Id = "x",
            DisplayName = "English title",
            DisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "中文标题"
            }
        };

        var r = CommunityTemplateDisplayLabels.ResolveDisplayName(info, ts);
        Assert.Equal("中文标题", r);
    }

    [Fact]
    public void ResolveGameProfileTemplateTitle_uses_map_with_computed_baseline()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("zh-CN") };
        var template = new GameProfileTemplate
        {
            ProfileId = "p",
            DisplayName = "English",
            DisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "本地化"
            }
        };

        var r = CommunityTemplateDisplayLabels.ResolveGameProfileTemplateTitle(template, "English", ts);
        Assert.Equal("本地化", r);
    }
}
