using System.IO;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using Xunit;

namespace GamepadMapping.Tests.Models;

public class TemplateStorageKeyTests
{
    [Fact]
    public void ValidateCatalogFolderPathForSave_AllowsMultiSegment()
    {
        var normalized = TemplateStorageKey.ValidateCatalogFolderPathForSave("Roco Kingdom/Maxim");
        Assert.Equal("Roco Kingdom/Maxim", normalized);
    }

    [Fact]
    public void GetTemplateJsonPath_CombinesSegments()
    {
        var root = @"C:\templates";
        var path = AppPaths.TemplateCatalogPaths.GetTemplateJsonPath(root, "Game Name/Author", "my-profile");
        var expected = Path.Combine(root, "Game Name", "Author", "my-profile.json");
        Assert.Equal(expected, path);
    }
}
