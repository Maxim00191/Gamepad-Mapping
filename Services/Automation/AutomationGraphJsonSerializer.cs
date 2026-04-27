#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationGraphJsonSerializer : IAutomationGraphSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize(AutomationGraphDocument document) =>
        JsonSerializer.Serialize(document ?? new AutomationGraphDocument(), Options);

    public AutomationGraphDocument Deserialize(string json)
    {
        var doc = JsonSerializer.Deserialize<AutomationGraphDocument>(json, Options);
        return doc ?? new AutomationGraphDocument();
    }
}
