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
            log.Add("[find_image] missing_haystack_input");
            context.StoreProbeResult(node.Id, new AutomationImageProbeResult(false, 0, 0, 0, 0));
            return context.GetExecutionTarget(node.Id, "flow.out");
        }

        var confidence = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageConfidence, 0.85);
        var tolerance = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTolerance, 1 - confidence);
        var timeoutMs = (int)AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTimeoutMs, 500);
        var algorithmText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageAlgorithm);
        var algorithm = ParseAlgorithm(algorithmText);
        var options = new AutomationImageProbeOptions(tolerance, timeoutMs);
        var needle = AutomationImageProbe.TryLoadBitmapFromPath(needlePath);
        var sourceNode = context.Index.GetNode(source);
        var sourceRef = sourceNode is null
            ? AutomationLogFormatter.NodeId(source)
            : AutomationLogFormatter.NodeRef(sourceNode.NodeTypeId, sourceNode.Id);
        log.Add($"[find_image] haystack_source={sourceRef} haystack_origin=({originX},{originY}) haystack_size={bitmap.PixelWidth}x{bitmap.PixelHeight}");
        log.Add($"[find_image] needle_path={needlePath} needle_loaded={(needle is not null ? "true" : "false")} confidence={confidence:F2} tolerance={tolerance:F2} timeout_ms={timeoutMs} algorithm={algorithm}");
        var raw = context.Probe.ProbeAsync(bitmap, originX, originY, needle, options, algorithm, cancellationToken).GetAwaiter().GetResult();
        var result = raw with
        {
            MatchCount = raw.Matched ? 1 : 0,
            Confidence = raw.Matched ? Math.Clamp(confidence, 0, 1) : 0
        };
        context.StoreProbeResult(node.Id, result);
        log.Add($"[find_image] matched={result.Matched} match_screen=({result.MatchScreenXPx},{result.MatchScreenYPx}) count={result.MatchCount} confidence={result.Confidence:F2}");
        return context.GetExecutionTarget(node.Id, "flow.out");
    }

    private static AutomationVisionAlgorithmKind ParseAlgorithm(string raw)
    {
        if (string.Equals(raw, "color_threshold", StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.ColorThreshold;
        if (string.Equals(raw, "contour", StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.Contour;
        return AutomationVisionAlgorithmKind.TemplateMatch;
    }
}
