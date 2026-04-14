namespace GamepadMapperGUI.Models.ControllerVisual;

public sealed record ControllerVisualLayoutDescriptor(
    string LayoutKey,
    string SvgFileName,
    IReadOnlyList<ControllerVisualRegionDefinition> Regions,
    string? DisplayName = null);
