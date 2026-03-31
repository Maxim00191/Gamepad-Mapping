using System.Collections.Generic;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing;

public class ComboLeadSemanticsTests
{
    [Fact]
    public void InferFromMappings_Empty_ReturnsEmptySet()
    {
        var leads = ComboLeadSemantics.InferFromMappings(new List<MappingEntry>());
        Assert.Empty(leads);
    }

    [Fact]
    public void InferFromMappings_LeftShoulderPlusFace_IncludesShoulderExcludesFace()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            }
        };

        var leads = ComboLeadSemantics.InferFromMappings(mappings);

        Assert.Contains(GamepadButtons.LeftShoulder, leads);
        Assert.DoesNotContain(GamepadButtons.X, leads);
    }

    [Fact]
    public void ParseDeclaredNames_Null_ReturnsNull()
    {
        Assert.Null(ComboLeadSemantics.ParseDeclaredNames(null));
    }

    [Fact]
    public void ParseDeclaredNames_ValidNames_Parsed()
    {
        var parsed = ComboLeadSemantics.ParseDeclaredNames(new List<string> { "LeftShoulder", "RightShoulder" });
        Assert.NotNull(parsed);

        Assert.Contains(GamepadButtons.LeftShoulder, parsed);
        Assert.Contains(GamepadButtons.RightShoulder, parsed);
    }

    [Fact]
    public void ResolveLeads_ExplicitTemplate_UsesExplicitAndSkipsInference()
    {
        var explicitLeads = new HashSet<GamepadButtons> { GamepadButtons.Back };
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            }
        };

        var resolved = ComboLeadSemantics.ResolveLeads(mappings, explicitLeads);

        Assert.Same(explicitLeads, resolved);
        Assert.Contains(GamepadButtons.Back, resolved);
        Assert.DoesNotContain(GamepadButtons.LeftShoulder, resolved);
    }
}
