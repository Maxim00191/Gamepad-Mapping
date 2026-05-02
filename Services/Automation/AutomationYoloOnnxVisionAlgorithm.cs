#nullable enable

using System.Collections.Concurrent;
using System.IO;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using DnnNet = OpenCvSharp.Dnn.Net;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationYoloOnnxVisionAlgorithm : IAutomationVisionAlgorithm
{
    private static readonly ConcurrentDictionary<string, Lazy<OnnxNetEntry>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private sealed class OnnxNetEntry(DnnNet net)
    {
        public DnnNet Net { get; } = net;

        public object Sync { get; } = new();
    }

    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.YoloOnnx;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modelPath = frame.ProbeOptions.YoloOnnxModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        try
        {
            var entry = GetOrCreateEntry(modelPath.Trim());
            using var haystackBgr = AutomationBitmapSourceToOpenCvMat.ToBgrMat(frame.Image);
            lock (entry.Sync)
            {
                var result = DetectBestCenter(
                    entry.Net,
                    haystackBgr,
                    frame.ProbeOptions,
                    cancellationToken);
                return ValueTask.FromResult(result);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));
        }
    }

    private static OnnxNetEntry GetOrCreateEntry(string path) =>
        Cache.GetOrAdd(path, static p => new Lazy<OnnxNetEntry>(() =>
        {
            var n = CvDnn.ReadNetFromOnnx(p) ??
                    throw new InvalidOperationException($"yolo_onnx:failed_to_load:{p}");
            return new OnnxNetEntry(n);
        })).Value;

    private static AutomationVisionResult DetectBestCenter(
        DnnNet net,
        Mat haystackBgr,
        AutomationImageProbeOptions options,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(25, options.TimeoutMs));
        var inputSize = AutomationYoloOnnxInferenceDefaults.LetterboxInputSize;
        var (letterboxed, scale, padX, padY) = Letterbox(haystackBgr, inputSize);
        using (letterboxed)
        {
            using var blob = CvDnn.BlobFromImage(
                letterboxed,
                1.0 / 255.0,
                new Size(inputSize, inputSize),
                default,
                swapRB: true,
                crop: false);
            net.SetInput(blob);
            cancellationToken.ThrowIfCancellationRequested();
            using var output = net.Forward();
            if (DateTime.UtcNow >= deadline)
                return new AutomationVisionResult(false, 0, 0);

            return ParseYoloOutput(
                output,
                haystackBgr.Width,
                haystackBgr.Height,
                scale,
                padX,
                padY,
                options);
        }
    }

    private static (Mat Letterboxed, double Scale, int PadX, int PadY) Letterbox(Mat src, int targetSize)
    {
        var w = src.Width;
        var h = src.Height;
        var scale = Math.Min((double)targetSize / w, (double)targetSize / h);
        var nw = Math.Max(1, (int)Math.Round(w * scale));
        var nh = Math.Max(1, (int)Math.Round(h * scale));
        using var resized = new Mat();
        Cv2.Resize(src, resized, new Size(nw, nh));
        var padX = (targetSize - nw) / 2;
        var padY = (targetSize - nh) / 2;
        var dst = new Mat(targetSize, targetSize, MatType.CV_8UC3, Scalar.Black);
        resized.CopyTo(dst[new Rect(padX, padY, nw, nh)]);
        return (dst, scale, padX, padY);
    }

    private static AutomationVisionResult ParseYoloOutput(
        Mat output,
        int origW,
        int origH,
        double scale,
        int padX,
        int padY,
        AutomationImageProbeOptions options)
    {
        var confMin = (float)(1.0 - Math.Clamp(options.Tolerance01, 0, 0.9));
        var classFilter = options.YoloClassIdFilter;

        if (output.Empty())
            return new AutomationVisionResult(false, 0, 0);

        var boxes = new List<Rect>();
        var scores = new List<float>();

        if (output.Dims == 3 && output.Size(0) == 1)
        {
            var innerA = output.Size(1);
            var innerB = output.Size(2);
            var featCount = Math.Min(innerA, innerB);
            var anchorCount = Math.Max(innerA, innerB);
            if (innerA < innerB)
                CollectDetectionsFeaturesFirst(output, featCount, anchorCount, padX, padY, scale, origW, origH, confMin, classFilter, boxes, scores);
            else
                CollectDetectionsAnchorsFirst(output, featCount, anchorCount, padX, padY, scale, origW, origH, confMin, classFilter, boxes, scores);
        }
        else if (output.Dims == 2)
        {
            var featCount = Math.Min(output.Rows, output.Cols);
            var anchorCount = Math.Max(output.Rows, output.Cols);
            if (output.Rows < output.Cols)
                CollectDetectionsFeaturesFirst(output, featCount, anchorCount, padX, padY, scale, origW, origH, confMin, classFilter, boxes, scores);
            else
                CollectDetectionsAnchorsFirst(output, featCount, anchorCount, padX, padY, scale, origW, origH, confMin, classFilter, boxes, scores);
        }

        if (boxes.Count == 0)
            return new AutomationVisionResult(false, 0, 0);

        CvDnn.NMSBoxes(boxes, scores, confMin, AutomationYoloOnnxInferenceDefaults.NmsIoUThreshold, out var keep);
        if (keep.Length == 0)
            return new AutomationVisionResult(false, 0, 0);

        var bestIdx = keep[0];
        var best = scores[bestIdx];
        for (var k = 1; k < keep.Length; k++)
        {
            var i = keep[k];
            if (scores[i] > best)
            {
                best = scores[i];
                bestIdx = i;
            }
        }

        var r = boxes[bestIdx];
        var centerX = r.X + r.Width / 2;
        var centerY = r.Y + r.Height / 2;
        return new AutomationVisionResult(true, centerX, centerY, keep.Length, best);
    }

    private static void CollectDetectionsFeaturesFirst(
        Mat t,
        int features,
        int anchors,
        int padX,
        int padY,
        double scale,
        int origW,
        int origH,
        float confMin,
        int classFilter,
        List<Rect> boxes,
        List<float> scores)
    {
        var numClasses = features - 4;
        if (numClasses <= 0)
            return;

        for (var i = 0; i < anchors; i++)
        {
            float cx;
            float cy;
            float bw;
            float bh;
            if (t.Dims == 3)
            {
                cx = t.At<float>(0, 0, i);
                cy = t.At<float>(0, 1, i);
                bw = t.At<float>(0, 2, i);
                bh = t.At<float>(0, 3, i);
            }
            else
            {
                cx = t.At<float>(0, i);
                cy = t.At<float>(1, i);
                bw = t.At<float>(2, i);
                bh = t.At<float>(3, i);
            }

            var bestScore = 0f;
            var bestCls = -1;
            for (var c = 0; c < numClasses; c++)
            {
                var s = t.Dims == 3 ? t.At<float>(0, 4 + c, i) : t.At<float>(4 + c, i);
                if (s > bestScore)
                {
                    bestScore = s;
                    bestCls = c;
                }
            }

            if (bestScore < confMin)
                continue;
            if (classFilter >= 0 && bestCls != classFilter)
                continue;

            MapBox(cx, cy, bw, bh, padX, padY, scale, origW, origH, bestScore, boxes, scores);
        }
    }

    private static void CollectDetectionsAnchorsFirst(
        Mat t,
        int features,
        int anchors,
        int padX,
        int padY,
        double scale,
        int origW,
        int origH,
        float confMin,
        int classFilter,
        List<Rect> boxes,
        List<float> scores)
    {
        var numClasses = features - 4;
        if (numClasses <= 0)
            return;

        for (var i = 0; i < anchors; i++)
        {
            float cx;
            float cy;
            float bw;
            float bh;
            if (t.Dims == 3)
            {
                cx = t.At<float>(0, i, 0);
                cy = t.At<float>(0, i, 1);
                bw = t.At<float>(0, i, 2);
                bh = t.At<float>(0, i, 3);
            }
            else
            {
                cx = t.At<float>(i, 0);
                cy = t.At<float>(i, 1);
                bw = t.At<float>(i, 2);
                bh = t.At<float>(i, 3);
            }

            var bestScore = 0f;
            var bestCls = -1;
            for (var c = 0; c < numClasses; c++)
            {
                var s = t.Dims == 3 ? t.At<float>(0, i, 4 + c) : t.At<float>(i, 4 + c);
                if (s > bestScore)
                {
                    bestScore = s;
                    bestCls = c;
                }
            }

            if (bestScore < confMin)
                continue;
            if (classFilter >= 0 && bestCls != classFilter)
                continue;

            MapBox(cx, cy, bw, bh, padX, padY, scale, origW, origH, bestScore, boxes, scores);
        }
    }

    private static void MapBox(
        float cx,
        float cy,
        float bw,
        float bh,
        int padX,
        int padY,
        double scale,
        int origW,
        int origH,
        float score,
        List<Rect> boxes,
        List<float> scores)
    {
        var ox = (cx - padX) / scale;
        var oy = (cy - padY) / scale;
        var ow = bw / scale;
        var oh = bh / scale;

        ox = Math.Clamp(ox, 0, origW - 1);
        oy = Math.Clamp(oy, 0, origH - 1);

        var x1 = ox - ow / 2;
        var y1 = oy - oh / 2;
        var x2 = ox + ow / 2;
        var y2 = oy + oh / 2;

        var rx1 = (int)Math.Floor(Math.Clamp(Math.Min(x1, x2), 0, origW - 1));
        var ry1 = (int)Math.Floor(Math.Clamp(Math.Min(y1, y2), 0, origH - 1));
        var rx2 = (int)Math.Ceiling(Math.Clamp(Math.Max(x1, x2), 0, origW - 1));
        var ry2 = (int)Math.Ceiling(Math.Clamp(Math.Max(y1, y2), 0, origH - 1));

        boxes.Add(new Rect(rx1, ry1, Math.Max(1, rx2 - rx1), Math.Max(1, ry2 - ry1)));
        scores.Add(score);
    }
}
