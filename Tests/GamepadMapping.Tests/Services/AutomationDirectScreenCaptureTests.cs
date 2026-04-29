using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
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

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out var result);
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

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out var result);
        Assert.True(ok);
        Assert.Same(bmp, result.Bitmap);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_InProcessWindow_CallsProcessCapture()
    {
        var bmp = CreateBitmap();
        var metrics = new AutomationVirtualScreenMetrics(100, 200, 2, 2);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics);
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out var result);
        Assert.True(ok);
        Assert.Same(bmp, result.Bitmap);
        Assert.Equal(100, result.Metrics.PhysicalOriginX);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_InProcessWindow_PassesProcessIdWhenConfigured()
    {
        var bmp = CreateBitmap();
        var metrics = new AutomationVirtualScreenMetrics(100, 200, 2, 2);
        var resolvedTarget = AutomationProcessWindowTarget.From("MyGame", 4242);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics, resolvedTarget);
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 4242)))
            .Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame",
            [AutomationNodePropertyKeys.CaptureProcessId] = 4242
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out var result);

        Assert.True(ok);
        Assert.Equal(4242, result.ProcessTarget.ProcessId);
        mock.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_InProcessWindow_ResolvesLiveProcessId()
    {
        var bmp = CreateBitmap();
        var metrics = new AutomationVirtualScreenMetrics(100, 200, 2, 2);
        var resolvedTarget = AutomationProcessWindowTarget.From("MyGame", 4242);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics, resolvedTarget);
        var capture = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var processTargets = new Mock<IProcessTargetService>(MockBehavior.Strict);
        processTargets
            .Setup(s => s.CreateTargetFromDeclaredProcessName("MyGame"))
            .Returns(new ProcessInfo { ProcessName = "MyGame", ProcessId = 4242 });
        capture.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 4242)))
            .Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame",
            [AutomationNodePropertyKeys.CaptureProcessId] = 1111
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(
            CreateResolver(capture.Object),
            props,
            out var result,
            processTargets.Object);

        Assert.True(ok);
        Assert.Equal(4242, result.ProcessTarget.ProcessId);
        capture.VerifyAll();
        processTargets.VerifyAll();
    }

    [Fact]
    public void TryDirectCapture_InProcessRoi_CropsProcessWindowCapture()
    {
        var bmp = CreateBitmap(6, 6);
        var metrics = new AutomationVirtualScreenMetrics(100, 200, 6, 6);
        var wrapped = new AutomationVirtualScreenCaptureResult(bmp, metrics);
        var mock = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        mock.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(wrapped);

        var props = new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame",
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 102,
                ["y"] = 203,
                ["width"] = 2,
                ["height"] = 2
            }
        };

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out var result);

        Assert.True(ok);
        Assert.Equal(2, result.Bitmap.PixelWidth);
        Assert.Equal(2, result.Bitmap.PixelHeight);
        Assert.Equal(102, result.Metrics.PhysicalOriginX);
        Assert.Equal(203, result.Metrics.PhysicalOriginY);
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

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out _);
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

        var ok = AutomationDirectScreenCapture.TryDirectCapture(CreateResolver(mock.Object), props, out _);
        Assert.False(ok);
        mock.VerifyAll();
    }

    private static IAutomationScreenCaptureServiceResolver CreateResolver(IAutomationScreenCaptureService capture) =>
        new AutomationScreenCaptureServiceResolver(
            new Dictionary<string, IAutomationScreenCaptureService>(StringComparer.OrdinalIgnoreCase)
            {
                [AutomationCaptureApi.Gdi] = capture,
                [AutomationCaptureApi.DesktopDuplication] = capture
            },
            AutomationCaptureApi.Gdi);

    private static BitmapSource CreateBitmap(int width = 2, int height = 2)
    {
        var stride = width * 4;
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[stride * height], stride);
        if (bitmap.CanFreeze)
            bitmap.Freeze();

        return bitmap;
    }
}
