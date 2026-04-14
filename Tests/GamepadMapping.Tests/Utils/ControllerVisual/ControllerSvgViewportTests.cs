using System.Xml.Linq;
using Gamepad_Mapping.Utils.ControllerVisual;

namespace GamepadMapping.Tests.Utils.ControllerVisual;

public class ControllerSvgViewportTests
{
    [Fact]
    public void TryReadViewportFromSvgElement_viewBox_parses_width_height()
    {
        var svg = XElement.Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 250"/>""");
        Assert.True(ControllerSvgViewport.TryReadViewportFromSvgElement(svg, out var vp));
        Assert.Equal(300, vp.Width);
        Assert.Equal(250, vp.Height);
    }

    [Fact]
    public void TryReadViewportFromSvgElement_width_height_fallback_strips_px()
    {
        var svg = XElement.Parse("""<svg xmlns="http://www.w3.org/2000/svg" width="400px" height="200"/>""");
        Assert.True(ControllerSvgViewport.TryReadViewportFromSvgElement(svg, out var vp));
        Assert.Equal(400, vp.Width);
        Assert.Equal(200, vp.Height);
    }
}
