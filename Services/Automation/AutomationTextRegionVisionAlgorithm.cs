#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using OpenCvSharp;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationTextRegionVisionAlgorithm : IAutomationVisionAlgorithm
{
    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.TextRegion;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = frame.ProbeOptions.EffectiveTextDetectionOptions;
        using var bgr = AutomationBitmapSourceToOpenCvMat.ToBgrMat(frame.Image);
        if (bgr.Empty())
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        using var gray = new Mat();
        using var gradient = new Mat();
        using var binary = new Mat();
        using var closed = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Sobel(gray, gradient, MatType.CV_8U, 1, 0, 3);
        Cv2.Threshold(gradient, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        var kernelSize = new Size(
            Math.Max(1, options.MorphologyWidth),
            Math.Max(1, options.MorphologyHeight));
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, kernelSize);
        Cv2.MorphologyEx(binary, closed, MorphTypes.Close, kernel);

        cancellationToken.ThrowIfCancellationRequested();
        Cv2.FindContours(
            closed,
            out OpenCvSharp.Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var minArea = Math.Max(1, options.MinimumRegionAreaPx);
        Rect? best = null;
        var bestArea = 0;
        var count = 0;
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            var area = rect.Width * rect.Height;
            if (area < minArea || rect.Width < rect.Height)
                continue;

            count++;
            if (area > bestArea)
            {
                best = rect;
                bestArea = area;
            }
        }

        if (best is not { } match)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var centerX = match.X + match.Width / 2;
        var centerY = match.Y + match.Height / 2;
        var confidence = Math.Clamp(bestArea / (double)Math.Max(1, bgr.Width * bgr.Height), 0d, 1d);
        return ValueTask.FromResult(new AutomationVisionResult(
            true,
            centerX,
            centerY,
            count,
            confidence,
            match.X,
            match.Y,
            match.Width,
            match.Height));
    }
}
