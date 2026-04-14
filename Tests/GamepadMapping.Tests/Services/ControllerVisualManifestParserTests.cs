using Gamepad_Mapping.Services.ControllerVisual;
using GamepadMapperGUI.Models.ControllerVisual;

namespace GamepadMapping.Tests.Services;

public class ControllerVisualManifestParserTests
{
    [Fact]
    public void TryParse_validJson_preserves_region_order_and_logical_ids()
    {
        const string json = """
            {
              "layoutKey": "xbox",
              "svgFile": "Xbox.svg",
              "regions": [
                { "logicalId": "a", "svgElementId": "id_a" },
                { "logicalId": "b", "svgElementId": "id_b", "elementKind": "path" }
              ]
            }
            """;

        Assert.True(ControllerVisualManifestParser.TryParse(json, out var d));
        Assert.NotNull(d);
        Assert.Equal("xbox", d.LayoutKey);
        Assert.Equal("Xbox.svg", d.SvgFileName);
        Assert.Equal(2, d.Regions.Count);
        Assert.Equal("a", d.Regions[0].LogicalId);
        Assert.Equal("id_a", d.Regions[0].SvgElementId);
        Assert.Equal(ControllerVisualElementKind.Auto, d.Regions[0].ElementKind);
        Assert.Equal("b", d.Regions[1].LogicalId);
        Assert.Equal(ControllerVisualElementKind.Path, d.Regions[1].ElementKind);
    }

    [Fact]
    public void TryParse_emptyRegions_returns_false()
    {
        const string json = """{"layoutKey":"k","svgFile":"f.svg","regions":[]}""";
        Assert.False(ControllerVisualManifestParser.TryParse(json, out _));
    }
}
