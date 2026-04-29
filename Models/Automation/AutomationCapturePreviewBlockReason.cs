#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public enum AutomationCapturePreviewBlockReason
{
    None,
    NotCaptureNode,
    MissingProperties,
    CacheReference,
    InvalidRoi,
}
