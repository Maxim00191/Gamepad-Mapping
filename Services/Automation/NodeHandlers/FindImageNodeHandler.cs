#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class FindImageNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "perception.find_image";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, List<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var needlePath = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageNeedlePath);
        if (string.IsNullOrWhiteSpace(needlePath))
            throw new InvalidOperationException("find_image:needle_missing");

        var sourceNodeId = context.Index.GetDataSource(node.Id, "haystack.image");
        if (sourceNodeId is not { } source || !context.TryGetCapture(source, out var bitmap, out var originX, out var originY))
        {
            log.Add("find_image:no_haystack");
            context.StoreProbeResult(node.Id, new AutomationImageProbeResult(false, 0, 0, 0, 0));
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var confidence = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageConfidence, 0.85);
        var tolerance = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTolerance, 1 - confidence);
        var timeoutMs = (int)AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTimeoutMs, 500);
        var options = new AutomationImageProbeOptions(tolerance, timeoutMs);
        var needle = AutomationImageProbe.TryLoadBitmapFromPath(needlePath);
        var raw = context.Probe.Probe(bitmap, originX, originY, needle, options);
        var result = raw with
        {
            MatchCount = raw.Matched ? 1 : 0,
            Confidence = raw.Matched ? Math.Clamp(confidence, 0, 1) : 0
        };
        context.StoreProbeResult(node.Id, result);
        return context.GetExecutionTarget(node.Id, "flow.out");
    }
}
