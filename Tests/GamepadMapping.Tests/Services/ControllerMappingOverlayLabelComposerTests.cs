using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Services.ControllerVisual;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ControllerMappingOverlayLabelComposerTests
{
    private static readonly ControllerVisualService Visual = new();

    private readonly ControllerChordContextResolver _resolver = new(Visual);
    private readonly ControllerMappingOverlayLabelComposer _composer;

    public ControllerMappingOverlayLabelComposerTests()
    {
        _composer = new ControllerMappingOverlayLabelComposer(Visual, _resolver);
    }

    [Fact]
    public void Chord_partner_shows_display_chord_line_and_action()
    {
        var chord = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
            KeyboardKey = "Q"
        };
        var soloOnX = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "X" },
            KeyboardKey = "E"
        };
        var all = new List<MappingEntry> { chord, soloOnX };
        var elementMappings = Visual.GetMappingsForElement("btn_X", all).ToList();

        var snap = _composer.Compose(
            "btn_X",
            elementMappings,
            all,
            "shoulder_L",
            ControllerMappingOverlayPrimaryLabelMode.ActionSummary,
            overlayShowSecondary: true);

        Assert.Equal("Q", snap.PrimaryLabel);
        Assert.NotNull(snap.SecondaryLabel);
        Assert.Contains("Left Shoulder", snap.SecondaryLabel, StringComparison.Ordinal);
        Assert.Contains("Button X", snap.SecondaryLabel, StringComparison.Ordinal);
        Assert.True(snap.HasExtraMappings);
    }

    [Fact]
    public void Without_selection_uses_standard_first_mapping()
    {
        var soloOnX = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "X" },
            KeyboardKey = "E"
        };
        var all = new List<MappingEntry> { soloOnX };
        var elementMappings = Visual.GetMappingsForElement("btn_X", all).ToList();

        var snap = _composer.Compose(
            "btn_X",
            elementMappings,
            all,
            selectedElementId: null,
            ControllerMappingOverlayPrimaryLabelMode.ActionSummary,
            overlayShowSecondary: true);

        Assert.Equal("E", snap.PrimaryLabel);
        Assert.Null(snap.SecondaryLabel);
    }
}
