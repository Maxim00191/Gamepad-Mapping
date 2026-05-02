#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationLoopScopeIndex
{
    private readonly Dictionary<string, Guid> _loopNodeIdByLabel;

    public AutomationLoopScopeIndex(AutomationGraphDocument document)
    {
        _loopNodeIdByLabel = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.NodeTypeId, "automation.loop", StringComparison.Ordinal))
                continue;

            var raw = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.LoopScopeLabel);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var label = raw.Trim();
            if (!_loopNodeIdByLabel.ContainsKey(label))
                _loopNodeIdByLabel[label] = node.Id;
        }
    }

    public IReadOnlyDictionary<string, Guid> LoopNodeIdByLabel => _loopNodeIdByLabel;

    public bool TryGetLoopNodeId(string scopeLabel, out Guid loopNodeId) =>
        _loopNodeIdByLabel.TryGetValue(scopeLabel.Trim(), out loopNodeId);

    public string? NormalizeSelection(string? raw)
    {
        var trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0)
            return null;

        foreach (var label in _loopNodeIdByLabel.Keys)
        {
            if (string.Equals(label, trimmed, StringComparison.OrdinalIgnoreCase))
                return label;
        }

        return null;
    }

    public static bool HasDuplicateLoopScopeLabels(AutomationGraphDocument document)
    {
        var seen = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in document.Nodes)
        {
            if (!string.Equals(node.NodeTypeId, "automation.loop", StringComparison.Ordinal))
                continue;

            var raw = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.LoopScopeLabel);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var label = raw.Trim();
            if (seen.TryGetValue(label, out var first) && first != node.Id)
                return true;

            seen[label] = node.Id;
        }

        return false;
    }
}
