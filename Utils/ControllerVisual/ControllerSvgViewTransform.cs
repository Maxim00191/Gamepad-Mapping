using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils.ControllerVisual;

internal static class ControllerSvgViewTransform
{
    public static Transform CreateUniformCenteredToViewport(Rect drawingBounds, ControllerSvgViewport viewport)
    {
        if (!drawingBounds.Width.IsFinitePositive() || !drawingBounds.Height.IsFinitePositive() ||
            !viewport.Width.IsFinitePositive() || !viewport.Height.IsFinitePositive())
            return Transform.Identity;

        var vw = viewport.Width;
        var vh = viewport.Height;
        var s = Math.Min(vw / drawingBounds.Width, vh / drawingBounds.Height);
        var ox = (vw - s * drawingBounds.Width) / 2;
        var oy = (vh - s * drawingBounds.Height) / 2;

        var group = new TransformGroup();
        group.Children.Add(new TranslateTransform(-drawingBounds.Left, -drawingBounds.Top));
        group.Children.Add(new ScaleTransform(s, s));
        group.Children.Add(new TranslateTransform(ox, oy));
        return group;
    }

    private static bool IsFinitePositive(this double d) => d > 0 && !double.IsNaN(d) && !double.IsInfinity(d);
}
