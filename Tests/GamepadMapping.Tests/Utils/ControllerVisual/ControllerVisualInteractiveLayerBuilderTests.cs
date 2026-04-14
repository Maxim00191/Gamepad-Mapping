using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ShapesPath = System.Windows.Shapes.Path;
using System.Windows.Shapes;
using System.Xml.Linq;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models.ControllerVisual;

namespace GamepadMapping.Tests.Utils.ControllerVisual;

public class ControllerVisualInteractiveLayerBuilderTests
{
    [Fact]
    public void BuildIdElementIndex_first_id_wins_and_is_case_insensitive()
    {
        var svg = XElement.Parse(
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g id="dup"><path id="btn_A" d="M0 0 L1 0 L1 1 Z"/></g>
              <path id="BTN_A" d="M2 2 L3 2 L3 3 Z"/>
            </svg>
            """);

        var index = ControllerVisualInteractiveLayerBuilder.BuildIdElementIndex(svg);

        Assert.True(index.TryGetValue("btn_A", out var first));
        Assert.Equal("path", first.Name.LocalName);
        Assert.Contains("M0 0", ControllerSvgXml.AttributeIgnoreCase(first, "d")?.Value ?? "");
    }

    [Fact]
    public void Populate_inlineSvg_addsInteractivePath_forKnownRegion()
    {
        Exception? threadEx = null;
        int childCount = 0;
        object? tag = null;

        var thread = new Thread(() =>
        {
            try
            {
                var svg = XElement.Parse(
                    """
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 250">
                      <path id="btn_A" d="M0 0 L10 0 L10 10 Z"/>
                    </svg>
                    """);

                var layout = new ControllerVisualLayoutDescriptor(
                    "test",
                    "unused.svg",
                    [new ControllerVisualRegionDefinition("logical_a", "btn_A")]);

                var canvas = new Canvas();
                var pathStyle = new Style(typeof(ShapesPath));
                var rectStyle = new Style(typeof(Rectangle));

                ControllerVisualInteractiveLayerBuilder.Populate(
                    canvas,
                    svg,
                    layout,
                    pathStyle,
                    rectStyle,
                    (_, _) => { },
                    (_, _) => { },
                    (_, _) => { });

                childCount = canvas.Children.Count;
                if (canvas.Children.Count > 0 && canvas.Children[0] is ShapesPath p)
                {
                    tag = p.Tag;
                    Assert.NotNull(p.Data);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadEx);
        Assert.Equal(1, childCount);
        Assert.Equal("logical_a", tag);
    }
}
