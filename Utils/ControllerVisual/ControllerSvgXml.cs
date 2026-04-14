using System.Xml.Linq;

namespace Gamepad_Mapping.Utils.ControllerVisual;

internal static class ControllerSvgXml
{
    public static XAttribute? AttributeIgnoreCase(XElement e, string localName) =>
        e.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));
}
