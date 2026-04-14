#nullable enable

using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class LogicalControlMappingsCountPhrases
{
    public static string FormatMappingCount(int count, TranslationService loc)
    {
        return count switch
        {
            0 => loc["VisualEditorLogicalControlMappingsMappingCountZero"],
            1 => loc["VisualEditorLogicalControlMappingsMappingCountOne"],
            _ => string.Format(loc["VisualEditorLogicalControlMappingsMappingCountMany"], count)
        };
    }
}
