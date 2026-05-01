#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using OpenCvSharp;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationColorThresholdVisionAlgorithm : IAutomationVisionAlgorithm
{
    public AutomationVisionAlgorithmKind Kind => AutomationVisionAlgorithmKind.ColorThreshold;

    public ValueTask<AutomationVisionResult> ProcessAsync(AutomationVisionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = frame.ProbeOptions.EffectiveColorDetectionOptions;
        var bgr = AutomationBitmapSourceToOpenCvMat.GetOrCreateCachedBgrMat(frame.Image);
        if (bgr.Width <= 0 || bgr.Height <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        using var mask = new Mat();
        BuildMask(hsv, options, mask);

        var area = Cv2.CountNonZero(mask);
        if (area < Math.Max(1, options.MinimumAreaPx))
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        using var nzIdx = new Mat();
        Cv2.FindNonZero(mask, nzIdx);
        if (nzIdx.Rows <= 0)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var rect = Cv2.BoundingRect(nzIdx);
        var moments = Cv2.Moments(mask, true);
        if (moments.M00 < 1e-6)
            return ValueTask.FromResult(new AutomationVisionResult(false, 0, 0));

        var cx = (int)(moments.M10 / moments.M00);
        var cy = (int)(moments.M01 / moments.M00);

        return ValueTask.FromResult(new AutomationVisionResult(
            true,
            cx,
            cy,
            area,
            1d,
            rect.Left,
            rect.Top,
            Math.Max(1, rect.Width),
            Math.Max(1, rect.Height)));
    }

    private static void BuildMask(Mat hsv, AutomationColorDetectionOptions options, Mat mask)
    {
        var hMin = Math.Clamp(options.HueMin, 0, 179);
        var hMax = Math.Clamp(options.HueMax, 0, 179);
        var sMin = Math.Clamp(options.SaturationMin, 0, 255);
        var sMax = Math.Clamp(options.SaturationMax, 0, 255);
        var vMin = Math.Clamp(options.ValueMin, 0, 255);
        var vMax = Math.Clamp(options.ValueMax, 0, 255);

        using var partial = new Mat();
        if (hMin <= hMax)
        {
            Cv2.InRange(
                hsv,
                new Scalar(hMin, sMin, vMin),
                new Scalar(hMax, sMax, vMax),
                mask);
            return;
        }

        using var mLow = new Mat();
        using var mHigh = new Mat();
        Cv2.InRange(
            hsv,
            new Scalar(hMin, sMin, vMin),
            new Scalar(179, sMax, vMax),
            mLow);
        Cv2.InRange(
            hsv,
            new Scalar(0, sMin, vMin),
            new Scalar(hMax, sMax, vMax),
            mHigh);
        Cv2.BitwiseOr(mLow, mHigh, mask);
    }
}
