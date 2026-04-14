using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Services.Infrastructure;

namespace GamepadMapping.Tests.Utils;

public class LogicalControlMappingsCountPhrasesTests
{
    private readonly TranslationService _loc = new();

    [Fact]
    public void FormatMappingCount_Zero_UsesZeroKey()
    {
        var s = LogicalControlMappingsCountPhrases.FormatMappingCount(0, _loc);
        Assert.Equal(_loc["VisualEditorLogicalControlMappingsMappingCountZero"], s);
    }

    [Fact]
    public void FormatMappingCount_One_UsesSingularKey()
    {
        var s = LogicalControlMappingsCountPhrases.FormatMappingCount(1, _loc);
        Assert.Equal(_loc["VisualEditorLogicalControlMappingsMappingCountOne"], s);
    }

    [Fact]
    public void FormatMappingCount_Many_InterpolatesCount()
    {
        var s = LogicalControlMappingsCountPhrases.FormatMappingCount(7, _loc);
        Assert.Equal(string.Format(_loc["VisualEditorLogicalControlMappingsMappingCountMany"], 7), s);
    }
}
