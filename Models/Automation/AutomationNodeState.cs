using System.Text.Json.Nodes;

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeState
{
    public Guid Id { get; set; }

    public string NodeTypeId { get; set; } = "";

    public double X { get; set; }

    public double Y { get; set; }

    public JsonObject? Properties { get; set; }
}
