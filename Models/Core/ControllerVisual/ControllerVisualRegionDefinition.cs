namespace GamepadMapperGUI.Models.ControllerVisual;

public sealed record ControllerVisualRegionDefinition(
    string LogicalId,
    string SvgElementId,
    ControllerVisualElementKind ElementKind = ControllerVisualElementKind.Auto);
