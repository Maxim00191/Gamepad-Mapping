using System.Linq;
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

    [Fact]
    public void GetMappings_IncludesRightTriggerChordWhenFromTypeIsButton()
    {
        var simple = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.RightTrigger, Value = "RightTrigger" }
        };
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightTrigger + B" }
        };
        var dpadChord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightTrigger + DPadLeft" }
        };

        var all = new[] { simple, chord, dpadChord };
        var forRt = _query.GetMappingsForLogicalControl("trigger_R", all);

        Assert.Equal(3, forRt.Count);
        Assert.Same(simple, forRt[0]);
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_ButtonChordWithRightTriggerFirst_ReturnsTriggerR()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightTrigger + B" }
        };

        Assert.Equal("trigger_R", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void FormatInputLine_RightTriggerChordUsesDisplayNames()
    {
        var visual = new ControllerVisualService();
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightTrigger + B" }
        };

        var line = ControllerMappingFromDisplayFormatter.FormatInputLine(visual, m);
        Assert.Contains("Right Trigger", line);
        Assert.Contains("Button B", line);
    }

    [Fact]
    public void GetMappingsForElement_LeftStick_IncludesDirectionalAndLegacyWholeStickValues()
    {
        var visual = new ControllerVisualService();
        var up = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" }
        };
        var legacy = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "LeftThumbstick" }
        };
        var all = new[] { up, legacy };

        var forStick = visual.GetMappingsForElement("thumbStick_L", all).ToList();

        Assert.Equal(2, forStick.Count);
    }

    [Fact]
    public void GetMappingsForElement_RightStick_IncludesAxisMouseLookStyleValues()
    {
        var visual = new ControllerVisualService();
        var x = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "X" }
        };
        var y = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "Y" }
        };
        var all = new[] { x, y };

        var forStick = visual.GetMappingsForElement("thumbStick_R", all).ToList();

        Assert.Equal(2, forStick.Count);
    }

    [Fact]
    public void GetMappingsForLogicalControl_LeftStick_CountMatchesAggregatedElementQuery()
    {
        var up = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" }
        };
        var down = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Down" }
        };
        var all = new[] { up, down };

        var forStick = _query.GetMappingsForLogicalControl("thumbStick_L", all);

        Assert.Equal(2, forStick.Count);
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_LeftThumbstickDirection_ReturnsThumbStickSurfaceId()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Left" }
        };

        Assert.Equal("thumbStick_L", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_RightThumbstickAxis_ReturnsThumbStickSurfaceId()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "X" }
        };

        Assert.Equal("thumbStick_R", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void GetMappingsForLogicalControl_TouchpadGesture_IncludesMapping()
    {
        var swipe = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "SWIPE_UP" }
        };
        var all = new[] { swipe };

        var forTouch = _query.GetMappingsForLogicalControl("btn_touchpad", all);

        Assert.Single(forTouch);
        Assert.Same(swipe, forTouch[0]);
    }

    [Fact]
    public void ResolvePrimaryLogicalControlIdForMapping_TouchpadGesture_ReturnsTouchpadLogicalId()
    {
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "SWIPE_LEFT" }
        };

        Assert.Equal("btn_touchpad", _query.ResolvePrimaryLogicalControlIdForMapping(m));
    }

    [Fact]
    public void FormatInputLine_LeftThumbstickDirection_UsesStickSurfaceDisplayName()
    {
        var visual = new ControllerVisualService();
        var m = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" }
        };

        var line = ControllerMappingFromDisplayFormatter.FormatInputLine(visual, m);

        Assert.Equal("Left Thumbstick", line);
    }
}
