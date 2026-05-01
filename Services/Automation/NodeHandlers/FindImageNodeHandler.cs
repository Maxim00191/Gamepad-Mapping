#nullable enable

using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Automation.NodeHandlers;

public sealed class FindImageNodeHandler : IAutomationRuntimeNodeHandler
{
    public string NodeTypeId => "perception.find_image";

    public Guid? Execute(AutomationRuntimeContext context, AutomationNodeState node, IList<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var needlePath = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageNeedlePath);
        var yoloOnnxPathRaw = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageYoloOnnxPath);
        var algorithmText = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageAlgorithm);
        var algorithm = AutomationVisionAlgorithmStorage.ParseFindImageAlgorithmKind(algorithmText);
        var requiresNeedle = AutomationVisionAlgorithmRequirements.RequiresNeedleImage(algorithm);
        var alternateNeedlePathList = AutomationNodePropertyReader.ReadStringList(
            node.Properties,
            AutomationNodePropertyKeys.FindImageAlternateNeedlePaths);
        if (requiresNeedle && string.IsNullOrWhiteSpace(needlePath) && alternateNeedlePathList.Count == 0)
            throw new InvalidOperationException("find_image:needle_missing");
        string? resolvedYoloOnnxPath = null;
        if (AutomationVisionAlgorithmRequirements.RequiresYoloOnnxModel(algorithm))
        {
            if (!AutomationYoloOnnxPaths.TryResolveEffectiveModelPath(yoloOnnxPathRaw, out var r))
                throw new InvalidOperationException("find_image:yolo_model_missing");
            resolvedYoloOnnxPath = r;
        }

        var sourceNodeId = context.Index.GetDataSource(node.Id, AutomationPortIds.HaystackImage);
        if (sourceNodeId is not { } source ||
            !context.TryGetCapture(
                source,
                out var bitmap,
                out var originX,
                out var originY,
                out AutomationProcessWindowTarget captureTargetProcess))
        {
            if (context.VerboseExecutionLog)
                log.Add("[find_image] missing_haystack_input");
            context.StoreProbeResult(node.Id, new AutomationImageProbeResult(false, 0, 0, 0, 0, 0));
            return context.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
        }

        context.StoreCapture(node.Id, bitmap, originX, originY, captureTargetProcess);

        var confidence = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageConfidence, 0.85);
        var tolerance = AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTolerance, 1 - confidence);
        var timeoutMs = (int)AutomationNodePropertyReader.ReadDouble(node.Properties, AutomationNodePropertyKeys.FindImageTimeoutMs, 500);
        var yoloClassId = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageYoloClassId, -1);
        var colorOptions = ReadColorDetectionOptions(node);
        var textOptions = ReadTextDetectionOptions(node);
        var ocrPhraseOptions = ReadOcrPhraseMatchOptions(node);
        var effectiveYoloPathForProbe = resolvedYoloOnnxPath;
        var templateMinCorrelation = AutomationImageProbeOptions.CombineTemplateMatchMinNormalizedCorrelation(tolerance, confidence);
        var options = new AutomationImageProbeOptions(
            tolerance,
            timeoutMs,
            effectiveYoloPathForProbe,
            yoloClassId,
            colorOptions,
            textOptions,
            ocrPhraseOptions,
            templateMinCorrelation);
        var needleChain = requiresNeedle
            ? BuildNeedleChain(needlePath, alternateNeedlePathList, context)
            : new List<(string RawPath, string? ResolvedPath, BitmapSource? Bitmap)>();
        var sourceNode = context.Index.GetNode(source);
        var sourceRef = sourceNode is null
            ? AutomationLogFormatter.NodeId(source)
            : AutomationLogFormatter.NodeRef(sourceNode.NodeTypeId, sourceNode.Id);
        if (context.VerboseExecutionLog)
        {
            log.Add($"[find_image] haystack_source={sourceRef} haystack_origin=({originX},{originY}) haystack_size={bitmap.PixelWidth}x{bitmap.PixelHeight}");
            log.Add($"[find_image] needle_path={needlePath} alternates={alternateNeedlePathList.Count} yolo_onnx={(effectiveYoloPathForProbe ?? "(none)")} yolo_class_id={yoloClassId} confidence={confidence:F2} tolerance={tolerance:F2} timeout_ms={timeoutMs} algorithm={algorithm}");
            if (requiresNeedle)
            {
                for (var i = 0; i < needleChain.Count; i++)
                {
                    var (raw, resolved, loaded) = needleChain[i];
                    log.Add(
                        $"[find_image] needle[{i}] raw={raw} resolved={(resolved ?? "(none)")} loaded={(loaded is not null ? "true" : "false")}");
                }
            }
            if (AutomationVisionAlgorithmRequirements.UsesColorDetectionOptions(algorithm))
                log.Add($"[find_image] color_hsv=({colorOptions.HueMin}-{colorOptions.HueMax},{colorOptions.SaturationMin}-{colorOptions.SaturationMax},{colorOptions.ValueMin}-{colorOptions.ValueMax}) min_area={colorOptions.MinimumAreaPx}");
            if (AutomationVisionAlgorithmRequirements.UsesTextDetectionOptions(algorithm))
                log.Add($"[find_image] text_min_area={textOptions.MinimumRegionAreaPx} morphology={textOptions.MorphologyWidth}x{textOptions.MorphologyHeight}");
            if (AutomationVisionAlgorithmRequirements.RequiresOcrPhraseList(algorithm))
                log.Add($"[find_image] ocr_max_long_edge_px={(ocrPhraseOptions.MaxLongEdgePx <= 0 ? "default" : ocrPhraseOptions.MaxLongEdgePx.ToString())} ocr_case_sensitive={ocrPhraseOptions.CaseSensitive}");
        }
        if (requiresNeedle && needleChain.TrueForAll(e => e.Bitmap is null))
        {
            if (context.VerboseExecutionLog)
                log.Add("[find_image] missing_template_needle => matched=false");
            context.StoreProbeResult(node.Id, new AutomationImageProbeResult(false, 0, 0, 0, 0, 0));
            return context.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
        }

        AutomationImageProbeResult result;
        if (!requiresNeedle)
        {
            var single = context.Probe
                .ProbeAsync(bitmap, originX, originY, null, options, algorithm, cancellationToken)
                .GetAwaiter()
                .GetResult();
            result = single with
            {
                MatchCount = single.Matched ? 1 : 0,
                Confidence = single.Matched ? Math.Clamp(confidence, 0, 1) : 0
            };
        }
        else
        {
            AutomationImageProbeResult? chosen = null;
            foreach (var entry in needleChain)
            {
                if (entry.Bitmap is null)
                    continue;

                var raw = context.Probe
                    .ProbeAsync(bitmap, originX, originY, entry.Bitmap, options, algorithm, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                var adjusted = raw with
                {
                    MatchCount = raw.Matched ? 1 : 0,
                    Confidence = raw.Matched ? Math.Clamp(confidence, 0, 1) : 0
                };
                if (adjusted.Matched)
                {
                    chosen = adjusted;
                    break;
                }

                chosen ??= adjusted;
            }

            result = chosen ?? new AutomationImageProbeResult(false, 0, 0, 0, 0, 0);
        }
        context.StoreProbeResult(node.Id, result);
        if (context.VerboseExecutionLog)
            log.Add($"[find_image] matched={result.Matched} match_screen=({result.MatchScreenXPx},{result.MatchScreenYPx}) count={result.MatchCount} confidence={result.Confidence:F2} raw_correlation={result.BestTemplateCorrelation:F2}");
        return context.GetExecutionTarget(node.Id, AutomationPortIds.FlowOut);
    }

    private static List<(string RawPath, string? ResolvedPath, BitmapSource? Bitmap)> BuildNeedleChain(
        string primaryPath,
        IReadOnlyList<string> alternatePaths,
        AutomationRuntimeContext context)
    {
        var chain = new List<(string, string?, BitmapSource?)>(1 + alternatePaths.Count);
        AddResolved(primaryPath, chain, context);
        foreach (var alt in alternatePaths)
            AddResolved(alt, chain, context);
        return chain;
    }

    private static void AddResolved(
        string rawPath,
        List<(string RawPath, string? ResolvedPath, BitmapSource? Bitmap)> chain,
        AutomationRuntimeContext context)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        var resolved = AutomationNeedlePathResolver.ResolveExistingFilePath(rawPath);
        var bmp = resolved is not null ? context.NeedleCache.GetOrLoadExistingFile(resolved) : null;
        chain.Add((rawPath, resolved, bmp));
    }

    private static AutomationColorDetectionOptions ReadColorDetectionOptions(AutomationNodeState node)
    {
        var defaults = AutomationColorDetectionOptions.Default;
        var baseline = new AutomationColorDetectionOptions(
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorHueMin, defaults.HueMin), 0, 179),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorHueMax, defaults.HueMax), 0, 179),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorSaturationMin, defaults.SaturationMin), 0, 255),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorSaturationMax, defaults.SaturationMax), 0, 255),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorValueMin, defaults.ValueMin), 0, 255),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorValueMax, defaults.ValueMax), 0, 255),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageColorMinimumAreaPx, defaults.MinimumAreaPx), 1, 1_000_000));
        var targetHex = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageColorTargetHex);
        return AutomationColorSelectionParser.ApplyTargetHex(baseline, targetHex);
    }

    private static AutomationTextDetectionOptions ReadTextDetectionOptions(AutomationNodeState node)
    {
        var defaults = AutomationTextDetectionOptions.Default;
        return new AutomationTextDetectionOptions(
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageTextMinimumRegionAreaPx, defaults.MinimumRegionAreaPx), 1, 1_000_000),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageTextMorphologyWidth, defaults.MorphologyWidth), 1, 99),
            ClampInt(AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageTextMorphologyHeight, defaults.MorphologyHeight), 1, 99),
            AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageTextQuery));
    }

    private static AutomationOcrPhraseMatchOptions ReadOcrPhraseMatchOptions(AutomationNodeState node)
    {
        var phrases = AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.FindImageOcrPhrases);
        var caseSensitive = AutomationNodePropertyReader.ReadBool(node.Properties, AutomationNodePropertyKeys.FindImageOcrCaseSensitive);
        var maxEdge = AutomationNodePropertyReader.ReadInt(node.Properties, AutomationNodePropertyKeys.FindImageOcrMaxLongEdgePx, 0);
        return new AutomationOcrPhraseMatchOptions(phrases, caseSensitive, maxEdge);
    }

    private static int ClampInt(int value, int min, int max) => Math.Clamp(value, min, max);
}
