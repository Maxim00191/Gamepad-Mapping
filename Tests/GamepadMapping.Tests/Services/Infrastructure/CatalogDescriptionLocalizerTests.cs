using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class CatalogDescriptionLocalizerTests
{
    [Fact]
    public void ApplyKeyboardAction_enUS_keeps_baseline_and_sets_resolved_from_baseline_when_only_zh_in_map()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("en-US") };
        var action = new KeyboardActionDefinition
        {
            Id = "attack",
            Description = "Attack",
            Descriptions = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "攻击",
            },
        };

        CatalogDescriptionLocalizer.ApplyKeyboardAction(action, ts);

        Assert.Equal("Attack", action.Description);
        Assert.Equal("Attack", action.ResolvedCatalogDescription);
    }

    [Fact]
    public void ApplyKeyboardAction_prefers_ui_culture_descriptions_map_over_description_key()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("en-US") };
        var action = new KeyboardActionDefinition
        {
            Id = "jump",
            DescriptionKey = "CatalogColumnId",
            Descriptions = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["en-US"] = "Jump",
            },
        };

        CatalogDescriptionLocalizer.ApplyKeyboardAction(action, ts);

        Assert.Equal("Jump", action.ResolvedCatalogDescription);
    }

    [Fact]
    public void ApplyTemplateOption_uses_displayNames_for_zh_ui()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("zh-CN") };
        var opt = new TemplateOption
        {
            ProfileId = "fight-maxim0191",
            CatalogSubfolder = "Roco Kingdom",
            DisplayNameBaseline = "Roco Kingdom World · Exploration – maxim0191",
            DisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "ロロ王国世界·探索 - maxim0191",
            },
            CatalogFolderDisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["zh-CN"] = "洛克王国",
            },
        };

        CatalogDescriptionLocalizer.ApplyTemplateOption(opt, ts);

        Assert.Equal("ロロ王国世界·探索 - maxim0191", opt.ResolvedDisplayName);
        Assert.Equal("洛克王国", opt.ResolvedCatalogFolderLabel);
        Assert.Contains("洛克王国", opt.TemplatePickerLabel, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTemplateOption_enUS_falls_back_to_baseline_when_no_en_map()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("en-US") };
        var opt = new TemplateOption
        {
            ProfileId = "x",
            CatalogSubfolder = "Roco Kingdom",
            DisplayNameBaseline = "English Title",
            DisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["zh-CN"] = "中文" },
        };

        CatalogDescriptionLocalizer.ApplyTemplateOption(opt, ts);

        Assert.Equal("English Title", opt.ResolvedDisplayName);
        Assert.Equal("Roco Kingdom", opt.ResolvedCatalogFolderLabel);
    }
}
