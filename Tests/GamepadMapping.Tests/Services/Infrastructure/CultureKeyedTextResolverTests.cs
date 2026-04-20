using System.Collections.Generic;
using System.Globalization;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class CultureKeyedTextResolverTests
{
    [Fact]
    public void TryPickForUiCulture_prefers_matching_locale()
    {
        var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["en-US"] = "Hello",
            ["zh-CN"] = "你好",
        };

        Assert.True(CultureKeyedTextResolver.TryPickForUiCulture(map, CultureInfo.GetCultureInfo("en-US"), out var v));
        Assert.Equal("Hello", v);
    }

    [Fact]
    public void TryPickFirstNonWhitespace_used_when_preferred_locale_empty()
    {
        var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = "仅中文",
        };

        Assert.False(CultureKeyedTextResolver.TryPickForUiCulture(map, CultureInfo.GetCultureInfo("en-US"), out _));
        Assert.True(CultureKeyedTextResolver.TryPickFirstNonWhitespace(map, out var fb));
        Assert.Equal("仅中文", fb);
    }
}
