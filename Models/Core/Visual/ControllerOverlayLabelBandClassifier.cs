#nullable enable

using System;
using System.Windows;

namespace Gamepad_Mapping.Models.Core.Visual;

public static class ControllerOverlayLabelBandClassifier
{
    public static ControllerOverlayLabelVerticalBand GetBand(string elementId)
    {
        if (string.IsNullOrEmpty(elementId))
            return ControllerOverlayLabelVerticalBand.Middle;

        if (elementId.StartsWith("trigger_", StringComparison.OrdinalIgnoreCase) ||
            elementId.StartsWith("shoulder_", StringComparison.OrdinalIgnoreCase))
            return ControllerOverlayLabelVerticalBand.TopCluster;

        if (elementId.Equals("btn_share", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_back", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_home", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_start", StringComparison.OrdinalIgnoreCase))
            return ControllerOverlayLabelVerticalBand.UpperCenter;

        if (elementId.StartsWith("dpad_", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_Y", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_A", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_X", StringComparison.OrdinalIgnoreCase) ||
            elementId.Equals("btn_B", StringComparison.OrdinalIgnoreCase))
            return ControllerOverlayLabelVerticalBand.BottomCluster;

        return ControllerOverlayLabelVerticalBand.Middle;
    }

    public static double GetBandTargetCenterY(
        ControllerOverlayLabelVerticalBand band,
        Rect viewport,
        double margin)
    {
        var innerH = Math.Max(0, viewport.Height - 2d * margin);
        var ratio = band switch
        {
            ControllerOverlayLabelVerticalBand.TopCluster => 0.11,
            ControllerOverlayLabelVerticalBand.UpperCenter => 0.30,
            ControllerOverlayLabelVerticalBand.Middle => 0.52,
            ControllerOverlayLabelVerticalBand.BottomCluster => 0.86,
            _ => 0.5
        };
        return viewport.Top + margin + innerH * ratio;
    }
}
