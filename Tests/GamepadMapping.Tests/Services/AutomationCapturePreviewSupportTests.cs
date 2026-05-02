#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationCapturePreviewSupportTests
{
    [Fact]
    public void TryGetPreviewableProperties_WrongNodeType_ReturnsNotCaptureNode()
    {
        var o = new JsonObject();
        var ok = AutomationCapturePreviewSupport.TryGetPreviewableProperties(
            "other.node",
            o,
            out _,
            out var reason);

        Assert.False(ok);
        Assert.Equal(AutomationCapturePreviewBlockReason.NotCaptureNode, reason);
    }

    [Fact]
    public void TryGetPreviewableProperties_CacheReference_ReturnsBlocked()
    {
        var id = Guid.NewGuid();
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureCacheRefNodeId] = id.ToString(),
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["width"] = 4,
                ["height"] = 4,
            },
        };

        var ok = AutomationCapturePreviewSupport.TryGetPreviewableProperties(
            AutomationNodeTypeIds.CaptureScreen,
            o,
            out _,
            out var reason);

        Assert.False(ok);
        Assert.Equal(AutomationCapturePreviewBlockReason.CacheReference, reason);
    }

    [Fact]
    public void TryGetPreviewableProperties_RoiMissingRect_ReturnsInvalidRoi()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
        };

        var ok = AutomationCapturePreviewSupport.TryGetPreviewableProperties(
            AutomationNodeTypeIds.CaptureScreen,
            o,
            out _,
            out var reason);

        Assert.False(ok);
        Assert.Equal(AutomationCapturePreviewBlockReason.InvalidRoi, reason);
    }

    [Fact]
    public void TryGetPreviewableProperties_RoiValid_Succeeds()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 1,
                ["y"] = 2,
                ["width"] = 10,
                ["height"] = 20,
            },
        };

        var ok = AutomationCapturePreviewSupport.TryGetPreviewableProperties(
            AutomationNodeTypeIds.CaptureScreen,
            o,
            out var props,
            out var reason);

        Assert.True(ok);
        Assert.Equal(AutomationCapturePreviewBlockReason.None, reason);
        Assert.Same(o, props);
    }

    [Fact]
    public void SuggestInspectorLiveByDefault_Roi_IsTrue()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["width"] = 4,
                ["height"] = 4,
            },
        };

        Assert.True(AutomationCapturePreviewSupport.SuggestInspectorLiveByDefault(o));
    }

    [Fact]
    public void SuggestInspectorLiveByDefault_FullScreen_IsFalse()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
        };

        Assert.False(AutomationCapturePreviewSupport.SuggestInspectorLiveByDefault(o));
    }

    [Fact]
    public void FormatCaptureStatus_Roi_UsesRoiFormatKey()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 1,
                ["y"] = 2,
                ["width"] = 3,
                ["height"] = 4,
            },
        };

        var text = AutomationCapturePreviewSupport.FormatCaptureStatus(o, static key =>
            key == "AutomationRoiPreview_StatusRoiFormat" ? "ROI {0} {1} {2} {3}" : key);

        Assert.Equal("ROI 1 2 3 4", text);
    }

    [Fact]
    public void FormatCaptureStatus_InProcessWindow_UsesProcessWindowFormatKey()
    {
        var o = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        };

        var text = AutomationCapturePreviewSupport.FormatCaptureStatus(
            o,
            static key => key == "AutomationRoiPreview_StatusProcessWindowFormat" ? "INPROC {0}" : key);

        Assert.Equal("INPROC MyGame", text);
    }
}
