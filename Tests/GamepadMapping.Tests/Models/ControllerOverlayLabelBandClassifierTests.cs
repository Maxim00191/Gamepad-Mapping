using System.Windows;
using Gamepad_Mapping.Models.Core.Visual;
using Xunit;

namespace GamepadMapping.Tests.Models;

public class ControllerOverlayLabelBandClassifierTests
{
    [Theory]
    [InlineData("trigger_L", ControllerOverlayLabelVerticalBand.TopCluster)]
    [InlineData("shoulder_R", ControllerOverlayLabelVerticalBand.TopCluster)]
    [InlineData("dpad_U", ControllerOverlayLabelVerticalBand.BottomCluster)]
    [InlineData("btn_A", ControllerOverlayLabelVerticalBand.BottomCluster)]
    [InlineData("btn_share", ControllerOverlayLabelVerticalBand.UpperCenter)]
    [InlineData("thumb_L", ControllerOverlayLabelVerticalBand.Middle)]
    public void GetBand_ClassifiesKnownIds(string id, ControllerOverlayLabelVerticalBand expected) =>
        Assert.Equal(expected, ControllerOverlayLabelBandClassifier.GetBand(id));

    [Fact]
    public void GetBandTargetCenterY_TopCluster_IsAboveBottomCluster()
    {
        var viewport = new Rect(0, 0, 400, 300);
        const double margin = 12d;
        var top = ControllerOverlayLabelBandClassifier.GetBandTargetCenterY(
            ControllerOverlayLabelVerticalBand.TopCluster, viewport, margin);
        var bottom = ControllerOverlayLabelBandClassifier.GetBandTargetCenterY(
            ControllerOverlayLabelVerticalBand.BottomCluster, viewport, margin);
        Assert.True(top < bottom);
    }
}
