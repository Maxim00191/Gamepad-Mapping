using Gamepad_Mapping.Services.ControllerVisual;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ControllerChordContextResolverTests
{
    private static readonly ControllerVisualService Visual = new();

    private readonly ControllerChordContextResolver _resolver = new(Visual);

    [Fact]
    public void GetChordParticipantElementIds_Includes_other_buttons_in_chord()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
            KeyboardKey = "Q"
        };
        var mappings = new[] { chord };

        var ids = _resolver.GetChordParticipantElementIds("shoulder_L", mappings);

        Assert.Contains("btn_X", ids);
    }

    [Fact]
    public void GetChordParticipantElementIds_Empty_when_no_selection()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
            KeyboardKey = "Q"
        };

        var ids = _resolver.GetChordParticipantElementIds(null, new[] { chord });

        Assert.Empty(ids);
    }

    [Fact]
    public void FindChordMappingBetween_Returns_shared_chord_mapping()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
            KeyboardKey = "Q"
        };
        var mappings = new[] { chord };

        var found = _resolver.FindChordMappingBetween("shoulder_L", "btn_X", mappings);

        Assert.Same(chord, found);
    }

    [Fact]
    public void FindChordMappingBetween_Returns_null_for_same_element()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
            KeyboardKey = "Q"
        };

        Assert.Null(_resolver.FindChordMappingBetween("btn_X", "btn_X", new[] { chord }));
    }
}
