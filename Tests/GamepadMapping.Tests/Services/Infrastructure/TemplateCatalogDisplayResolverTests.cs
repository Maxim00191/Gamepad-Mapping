using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class TemplateCatalogDisplayResolverTests
{
    [Fact]
    public void Resolve_enUS_uses_baseline_when_map_only_has_zhCn()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("en-US") };
        var r = TemplateCatalogDisplayResolver.Resolve(
            "Camera: yaw",
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["zh-CN"] = "水平旋转视角" },
            resourceKey: null,
            ts);
        Assert.Equal("Camera: yaw", r);
    }

    [Fact]
    public void Resolve_zhCn_prefers_map_over_baseline()
    {
        var ts = new TranslationService { Culture = CultureInfo.GetCultureInfo("zh-CN") };
        var r = TemplateCatalogDisplayResolver.Resolve(
            "Camera: yaw",
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["zh-CN"] = "水平旋转视角" },
            resourceKey: null,
            ts);
        Assert.Equal("水平旋转视角", r);
    }
}
