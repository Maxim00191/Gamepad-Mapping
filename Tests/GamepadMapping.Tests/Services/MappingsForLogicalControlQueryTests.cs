using Gamepad_Mapping.Services.ControllerVisual;
using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Services;

public class MappingsForLogicalControlQueryTests
{
    private readonly MappingsForLogicalControlQuery _query = new(new ControllerVisualService());

    [Fact]
    public void GetMappings_IncludesExactMatchAndSortsSimpleBeforeChord()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Start+DPadUp" }
        };
        var simple = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Start" }
        };
        var other = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };

        var all = new[] { chord, simple, other };
        var forStart = _query.GetMappingsForLogicalControl("btn_start", all);

        Assert.Equal(2, forStart.Count);
        Assert.Same(simple, forStart[0]);
        Assert.Same(chord, forStart[1]);
    }

    [Fact]
    public void MappingInvolvesLogicalControl_FalseWhenTypeMismatch()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Start" }
        };

        Assert.False(_query.MappingInvolvesLogicalControl(m, "trigger_L"));
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_ReturnsFirstChordPartId()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Start+DPadUp" }
        };

        Assert.Equal("btn_start", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_SingleBinding()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Back" }
        };

        Assert.Equal("btn_back", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void FormatInputLine_JoinsChordPartsWithSeparator()
    {
        var visual = new ControllerVisualService();
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Start+DPadUp" }
        };

        var line = ControllerMappingFromDisplayFormatter.FormatInputLine(visual, m);
        Assert.Contains("Start", line);
        Assert.Contains("D-Pad Up", line);
    }
}
