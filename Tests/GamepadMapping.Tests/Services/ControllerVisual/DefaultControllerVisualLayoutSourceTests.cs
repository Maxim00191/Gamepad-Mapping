using Gamepad_Mapping.Services.ControllerVisual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Services.ControllerVisual;

public class DefaultControllerVisualLayoutSourceTests
{
    [Fact]
    public void GetLayoutForGamepadApi_XInput_ReturnsXboxLayout()
    {
        var source = new DefaultControllerVisualLayoutSource();

        var layout = source.GetLayoutForGamepadApi(GamepadSourceApiIds.XInput);

        Assert.Equal("xbox", layout.LayoutKey);
        Assert.Equal(ControllerSvgConstants.XboxControllerSvgFileName, layout.SvgFileName);
    }

    [Fact]
    public void GetLayoutForGamepadApi_PlayStation_ReturnsDualSenseLayout()
    {
        var source = new DefaultControllerVisualLayoutSource();

        var layout = source.GetLayoutForGamepadApi(GamepadSourceApiIds.PlayStation);

        Assert.Equal("dualsense", layout.LayoutKey);
        Assert.Equal(ControllerSvgConstants.DualSenseControllerSvgFileName, layout.SvgFileName);
        Assert.Contains(layout.Regions, static region => region.LogicalId == "btn_touchpad");
    }

    [Fact]
    public void GetLayoutForGamepadApi_DualSenseAlias_UsesNormalization()
    {
        var source = new DefaultControllerVisualLayoutSource(
            normalizeGamepadApiId: static apiId =>
                string.Equals(apiId, GamepadSourceApiIds.DualSense, StringComparison.OrdinalIgnoreCase)
                    ? GamepadSourceApiIds.PlayStation
                    : apiId?.Trim() ?? GamepadSourceApiIds.XInput);

        var layout = source.GetLayoutForGamepadApi(GamepadSourceApiIds.DualSense);

        Assert.Equal("dualsense", layout.LayoutKey);
        Assert.Equal(ControllerSvgConstants.DualSenseControllerSvgFileName, layout.SvgFileName);
    }

    [Fact]
    public void GetActiveLayout_UsesConfiguredGamepadApiProvider()
    {
        var source = new DefaultControllerVisualLayoutSource(
            getActiveGamepadApiId: static () => GamepadSourceApiIds.PlayStation);

        var layout = source.GetActiveLayout();

        Assert.Equal("dualsense", layout.LayoutKey);
        Assert.Equal(ControllerSvgConstants.DualSenseControllerSvgFileName, layout.SvgFileName);
    }
}
