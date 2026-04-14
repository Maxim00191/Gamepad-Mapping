#nullable enable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Gamepad_Mapping.Models.Core.Visual;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerMappingOverlayLabelSizeEstimator
{
    public static Size Estimate(ControllerMappingOverlayItem item, double pixelsPerDip = 1d) =>
        item.StackPrimaryAndSecondary
            ? EstimateStacked(item.PrimaryLabel, item.SecondaryLabel, pixelsPerDip)
            : EstimateInline(item.PrimaryLabel, item.SecondaryLabel, pixelsPerDip);

    public static Size Estimate(string? primary, string? secondary, double pixelsPerDip = 1d) =>
        EstimateInline(primary, secondary, pixelsPerDip);

    private static Size EstimateInline(string? primary, string? secondary, double pixelsPerDip)
    {
        var primaryText = primary ?? string.Empty;
        var secondaryText = secondary ?? string.Empty;

        if (string.IsNullOrEmpty(primaryText) && string.IsNullOrEmpty(secondaryText))
            return new Size(24d, 18d);

        var primaryFt = CreateFormatted(primaryText, ControllerMappingOverlayLabelMetrics.PrimaryFontSize, FontWeights.Bold, pixelsPerDip);
        var secondaryFt = string.IsNullOrEmpty(secondaryText)
            ? null
            : CreateFormatted(secondaryText, ControllerMappingOverlayLabelMetrics.SecondaryFontSize, FontWeights.Normal, pixelsPerDip);

        var maxW = ControllerMappingOverlayLabelMetrics.MaxTextBlockWidth;
        var w1 = Math.Min(maxW, primaryFt.Width);
        var w2 = secondaryFt is null ? 0d : Math.Min(maxW, secondaryFt.Width);

        var h1 = primaryFt.Height;
        var h2 = secondaryFt?.Height ?? 0d;

        var contentW = w1 + (secondaryFt is null ? 0d : ControllerMappingOverlayLabelMetrics.SecondaryTextMarginLeft + w2);
        var contentH = Math.Max(h1, h2);

        return AddBorderPadding(contentW, contentH);
    }

    private static Size EstimateStacked(string? primary, string? secondary, double pixelsPerDip)
    {
        var primaryText = primary ?? string.Empty;
        var secondaryText = secondary ?? string.Empty;

        if (string.IsNullOrEmpty(primaryText) && string.IsNullOrEmpty(secondaryText))
            return new Size(24d, 18d);

        var primaryFt = CreateFormatted(primaryText, ControllerMappingOverlayLabelMetrics.PrimaryFontSize, FontWeights.Bold, pixelsPerDip);
        var maxW = ControllerMappingOverlayLabelMetrics.MaxTextBlockWidth;
        var w1 = Math.Min(maxW, primaryFt.Width);
        var h1 = primaryFt.Height;

        if (string.IsNullOrEmpty(secondaryText))
            return AddBorderPadding(w1, h1);

        var secondaryFt = CreateFormatted(secondaryText, ControllerMappingOverlayLabelMetrics.SecondaryFontSize, FontWeights.Normal, pixelsPerDip);
        var w2 = Math.Min(maxW, secondaryFt.Width);
        var h2 = secondaryFt.Height;

        var contentW = Math.Max(w1, w2);
        var contentH = h1 + ControllerMappingOverlayLabelMetrics.StackedSecondaryMarginTop + h2;

        return AddBorderPadding(contentW, contentH);
    }

    private static Size AddBorderPadding(double contentW, double contentH)
    {
        var padL = ControllerMappingOverlayLabelMetrics.BorderPaddingLeft;
        var padT = ControllerMappingOverlayLabelMetrics.BorderPaddingTop;
        var b = ControllerMappingOverlayLabelMetrics.BorderThickness;

        var innerW = contentW + padL * 2;
        var innerH = contentH + padT * 2;
        var outerW = innerW + b * 2;
        var outerH = innerH + b * 2;

        var bleed = ControllerMappingOverlayLabelMetrics.EstimatedLayoutBleed;
        var w = Math.Min(ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth, Math.Max(28d, outerW));
        return new Size(w, Math.Max(1d, outerH + bleed));
    }

    private static FormattedText CreateFormatted(string text, double size, FontWeight weight, double pixelsPerDip) =>
        new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily(ControllerMappingOverlayLabelMetrics.FontFamilyName),
                FontStyles.Normal,
                weight,
                FontStretches.Normal),
            size,
            Brushes.White,
            pixelsPerDip);
}
