#nullable enable

using System.Globalization;
using GamepadMapperGUI.Services.Infrastructure;
using Xunit;

namespace GamepadMapping.Tests.Services.Infrastructure;

public sealed class AppUiLocalizationTests
{
    [Fact]
    public void OptionalAlternateLanguageDescriptionCaption_NoApplication_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AppUiLocalization.OptionalAlternateLanguageDescriptionCaption());
    }

    [Fact]
    public void GetString_NoApplication_ReturnsKey()
    {
        Assert.Equal("SomeKey", AppUiLocalization.GetString("SomeKey"));
    }

    [Fact]
    public void EditorUiCulture_NoApplication_FallsBackToCurrentUiCulture()
    {
        var expected = CultureInfo.CurrentUICulture;
        Assert.Equal(expected.Name, AppUiLocalization.EditorUiCulture().Name);
    }
}
