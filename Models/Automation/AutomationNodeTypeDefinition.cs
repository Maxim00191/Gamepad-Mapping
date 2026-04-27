namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeTypeDefinition
{
    public required string Id { get; init; }

    public required string DisplayNameResourceKey { get; init; }

    public required string GlyphFontGlyph { get; init; }

    public required string SummaryResourceKey { get; init; }

    public required IReadOnlyList<AutomationPortDescriptor> InputPorts { get; init; }

    public required IReadOnlyList<AutomationPortDescriptor> OutputPorts { get; init; }
}
