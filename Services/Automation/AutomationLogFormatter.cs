#nullable enable

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationLogFormatter
{
    public static string NodeRef(string nodeTypeId, Guid nodeId) =>
        $"{nodeTypeId}#{ShortId(nodeId)}";

    public static string NodeId(Guid nodeId) => ShortId(nodeId);

    private static string ShortId(Guid id)
    {
        var text = id.ToString("N");
        return text.Length >= 8 ? text[..8] : text;
    }
}
