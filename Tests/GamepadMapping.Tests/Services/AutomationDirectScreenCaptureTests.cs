using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using Moq;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationDirectScreenCaptureTests
{
    [Fact]
    public void TryDirectCapture_RoiMode_CallsCaptureRectangle()
    {
        var bmp = CreateBitmap();
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureRectanglePhysical(5, 6, 10, 11)).Returns(bmp);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 5,
                ["y"] = 6,
                ["width"] = 10,
                ["height"] = 11
            }
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(mock.Object, props, out var result);
        Assert.True(ok);
        Assert.Same(bmp, result.Bitmap);
        Assert.Equal(5, result.Metrics.PhysicalOriginX);
        Assert.Equal(6, result.Metrics.PhysicalOriginY);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_FullScreen_CallsVirtualScreen()
    {
        var bmp = CreateBitmap();
        var metrics = new AutomationVirtualScreenMetrics(0, 0, 2, 2);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics);
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureVirtualScreenPhysical()).Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.Screen
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(mock.Object, props, out var result);
        Assert.True(ok);
        Assert.Same(bmp, result.Bitmap);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_ProcessWindow_CallsProcessCapture()
    {
        var bmp = CreateBitmap();
        var metrics = new AutomationVirtualScreenMetrics(100, 200, 2, 2);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics);
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureProcessWindowPhysical("MyGame")).Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.ProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(mock.Object, props, out var result);
        Assert.True(ok);
        Assert.Same(bmp, result.Bitmap);
        Assert.Equal(100, result.Metrics.PhysicalOriginX);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_CacheRef_ReturnsFalseWithoutCallingCapture()
    {
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureCacheRefNodeId] = Guid.NewGuid().ToString(),
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(mock.Object, props, out _);
        Assert.False(ok);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_RoiInvalid_ReturnsFalse()
    {
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["width"] = 0,
                ["height"] = 0
            }
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(mock.Object, props, out _);
        Assert.False(ok);
        mock.VerifyAll();
    }

    private static BitmapSource CreateBitmap()
    {
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
        if (bitmap.CanFreeze)
            bitmap.Freeze();

        return bitmap;
    }
}
