using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Models;

public class MappingEntryOutputSummaryForControllerOverlayTests
{
    [Fact]
    public void Keyboard_CombinesDescriptionAndKey()
    {
        var m = new MappingEntry
        {
            Description = "Open Map",
            KeyboardKey = "M"
        };

        Assert.Equal("Open Map - M", m.OutputSummaryForControllerOverlay);
    }

    [Fact]
    public void Keyboard_KeyOnly_MatchesGridSummary()
    {
        var m = new MappingEntry { KeyboardKey = "M" };

        Assert.Equal("M", m.OutputSummaryForControllerOverlay);
        Assert.Equal(m.OutputSummaryForGrid, m.OutputSummaryForControllerOverlay);
    }

    [Fact]
    public void NonKeyboard_MatchesOutputSummaryForGrid()
    {
        var m = new MappingEntry
        {
            RadialMenu = new RadialMenuBinding { RadialMenuId = "hud" },
            Description = "Quick Menu"
        };

        Assert.Equal("Quick Menu", m.OutputSummaryForControllerOverlay);
        Assert.Equal(m.OutputSummaryForGrid, m.OutputSummaryForControllerOverlay);
    }
}
