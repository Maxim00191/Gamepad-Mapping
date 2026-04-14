using System.Windows.Media;
using System.Xml.Linq;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public sealed record ControllerSvgAlignedLoadResult(
    DrawingImage Image,
    ControllerSvgViewport Viewport,
    Transform InteractionLayerTransform,
    XElement SvgRoot);
