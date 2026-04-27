#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeLayoutMetricsService : IAutomationNodeLayoutMetricsService
{
    private const double MinNodeWidth = 236d;
    private const double MaxNodeWidth = 520d;
    private const double MinVisualHeight = 152d;
    private const double BaseContentMinWidth = 156d;
    private const double MaxContentMinWidth = 250d;
    private const double MinPortSectionWidth = 148d;
    private const double MaxPortSectionWidth = 290d;
    private const double PortLabelCharWidthEstimate = 6.1d;
    private const double PortSectionPadding = 24d;
    private const double InlineEditorWidthGain = 7d;
    private const double PortRowMinHeight = 28d;
    private const double PortRowStride = 18d;
    private const double PortRowTopBottomPadding = 10d;
    private const double BaseSettingsSectionMinHeight = 46d;
    private const double InlineEditorHeightStride = 34d;
    private const double MaxInlineEditorHeight = 170d;
    private const double NodeChromeVerticalPadding = 46d;

    public AutomationNodeLayoutMetrics Build(
        IReadOnlyList<string> inputPortLabels,
        IReadOnlyList<string> outputPortLabels,
        int inlineEditorCount)
    {
        var inputRowWidth = BuildPortSectionWidth(inputPortLabels);
        var outputRowWidth = BuildPortSectionWidth(outputPortLabels);
        var contentMinWidth = Math.Clamp(
            BaseContentMinWidth + (Math.Max(0, inlineEditorCount - 1) * InlineEditorWidthGain),
            BaseContentMinWidth,
            MaxContentMinWidth);
        var width = Math.Clamp(Math.Max(inputRowWidth, Math.Max(outputRowWidth, contentMinWidth)), MinNodeWidth, MaxNodeWidth);
        var outputPortRowMinHeight = BuildPortRowMinHeight(outputPortLabels.Count);
        var inputPortRowMinHeight = BuildPortRowMinHeight(inputPortLabels.Count);
        var settingsSectionMinHeight = BuildSettingsSectionMinHeight(inlineEditorCount);
        var visualMinHeight = Math.Max(
            MinVisualHeight,
            NodeChromeVerticalPadding + outputPortRowMinHeight + inputPortRowMinHeight + settingsSectionMinHeight);

        return new AutomationNodeLayoutMetrics
        {
            NodeWidth = width,
            VisualMinHeight = visualMinHeight,
            ContentMinWidth = contentMinWidth,
            OutputPortRowMinHeight = outputPortRowMinHeight,
            InputPortRowMinHeight = inputPortRowMinHeight,
            SettingsSectionMinHeight = settingsSectionMinHeight
        };
    }

    private static double BuildPortSectionWidth(IReadOnlyList<string> labels)
    {
        var maxLen = 0;
        foreach (var label in labels)
            maxLen = Math.Max(maxLen, label?.Length ?? 0);

        var estimated = PortSectionPadding + (maxLen * PortLabelCharWidthEstimate);
        return Math.Clamp(estimated, MinPortSectionWidth, MaxPortSectionWidth);
    }

    private static double BuildPortRowMinHeight(int portCount)
    {
        if (portCount <= 0)
            return PortRowMinHeight;

        return Math.Max(
            PortRowMinHeight,
            PortRowTopBottomPadding + ((portCount - 1) * PortRowStride) + PortRowTopBottomPadding);
    }

    private static double BuildSettingsSectionMinHeight(int inlineEditorCount)
    {
        if (inlineEditorCount <= 0)
            return 0d;

        var estimated = BaseSettingsSectionMinHeight + ((inlineEditorCount - 1) * InlineEditorHeightStride);
        return Math.Min(estimated, MaxInlineEditorHeight);
    }
}
