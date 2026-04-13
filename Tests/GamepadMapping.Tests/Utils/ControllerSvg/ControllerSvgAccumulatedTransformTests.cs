using System.Windows;
using System.Xml.Linq;
using Gamepad_Mapping.Utils.ControllerSvg;

namespace GamepadMapping.Tests.Utils.ControllerSvg;

public class ControllerSvgAccumulatedTransformTests
{
    [Fact]
    public void GetMatrix_nested_translate_accumulates()
    {
        var svg = XElement.Parse(
            """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g transform="translate(10 20)">
                <g transform="translate(5,0)">
                  <path id="p" d="M0 0 L10 0"/>
                </g>
              </g>
            </svg>
            """);

        var path = svg.Descendants().First(e => e.Name.LocalName.Equals("path", StringComparison.OrdinalIgnoreCase));
        var m = ControllerSvgAccumulatedTransform.GetMatrix(path);
        var p = m.Transform(new Point(0, 0));
        Assert.Equal(15, p.X, 9);
        Assert.Equal(20, p.Y, 9);
    }

    [Fact]
    public void GetMatrix_matrix_and_translate_composes()
    {
        var svg = XElement.Parse(
            """
            <svg xmlns="http://www.w3.org/2000/svg">
              <g transform="matrix(2 0 0 2 0 0)">
                <rect id="r" x="5" y="6" width="4" height="4" transform="translate(1 0)"/>
              </g>
            </svg>
            """);

        var rect = svg.Descendants().First(e => e.Name.LocalName.Equals("rect", StringComparison.OrdinalIgnoreCase));
        var m = ControllerSvgAccumulatedTransform.GetMatrix(rect);
        var c = m.Transform(new Point(7, 8));
        Assert.Equal(15, c.X, 9);
        Assert.Equal(16, c.Y, 9);
    }
}
