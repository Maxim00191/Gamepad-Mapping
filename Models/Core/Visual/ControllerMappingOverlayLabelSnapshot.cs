#nullable enable

namespace Gamepad_Mapping.Models.Core.Visual;

public readonly record struct ControllerMappingOverlayLabelSnapshot(
    string PrimaryLabel,
    string? SecondaryLabel,
    bool StackPrimaryAndSecondary,
    bool HasExtraMappings,
    string? OverlayToolTip,
    bool IsCombination);
