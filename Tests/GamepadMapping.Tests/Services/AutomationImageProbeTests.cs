#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using Moq;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationImageProbeTests
{
    [Fact]
    public async Task ProbeAsync_TemplateMatch_MissingNeedle_DoesNotInvokePipeline()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>(MockBehavior.Strict);
        var sut = new AutomationImageProbe(pipeline.Object);
        var haystack = CreateHaystack();
        var options = new AutomationImageProbeOptions(0.1, 500);

        var result = await sut.ProbeAsync(
            haystack,
            0,
            0,
            null,
            options,
            AutomationVisionAlgorithmKind.TemplateMatch,
            CancellationToken.None);

        Assert.False(result.Matched);
        pipeline.Verify(
            p => p.ProcessAsync(
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProbeAsync_ColorThreshold_NullNeedle_AddsHaystackOriginToCentroid()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.Is<AutomationVisionFrame>(f => f.Needle == null),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(true, 30, 40)));

        var sut = new AutomationImageProbe(pipeline.Object);
        var haystack = CreateHaystack();
        var options = new AutomationImageProbeOptions(0.1, 500);

        var result = await sut.ProbeAsync(
            haystack,
            100,
            200,
            null,
            options,
            AutomationVisionAlgorithmKind.ColorThreshold,
            CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(130, result.MatchScreenXPx);
        Assert.Equal(240, result.MatchScreenYPx);
    }

    [Fact]
    public async Task ProbeAsync_Contour_AddsHaystackOriginToCentroid()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.Contour,
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(true, 5, 6)));

        var sut = new AutomationImageProbe(pipeline.Object);
        var haystack = CreateHaystack();

        var result = await sut.ProbeAsync(
            haystack,
            10,
            12,
            null,
            new AutomationImageProbeOptions(0.1, 500),
            AutomationVisionAlgorithmKind.Contour,
            CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(15, result.MatchScreenXPx);
        Assert.Equal(18, result.MatchScreenYPx);
    }

    [Fact]
    public async Task ProbeAsync_OpenCvTemplateMatch_MissingNeedle_DoesNotInvokePipeline()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>(MockBehavior.Strict);
        var sut = new AutomationImageProbe(pipeline.Object);

        var result = await sut.ProbeAsync(
            CreateHaystack(),
            0,
            0,
            null,
            new AutomationImageProbeOptions(0.1, 500),
            AutomationVisionAlgorithmKind.OpenCvTemplateMatch,
            CancellationToken.None);

        Assert.False(result.Matched);
        pipeline.Verify(
            p => p.ProcessAsync(
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProbeAsync_YoloOnnx_AddsHaystackOriginToDetectionCenter()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.YoloOnnx,
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(true, 42, 43)));

        var sut = new AutomationImageProbe(pipeline.Object);

        var result = await sut.ProbeAsync(
            CreateHaystack(),
            7,
            8,
            null,
            new AutomationImageProbeOptions(0.1, 500, @"C:\fake\model.onnx"),
            AutomationVisionAlgorithmKind.YoloOnnx,
            CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(49, result.MatchScreenXPx);
        Assert.Equal(51, result.MatchScreenYPx);
    }

    [Fact]
    public async Task ProbeAsync_TextRegion_AddsHaystackOriginToRegionCenter()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.TextRegion,
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(true, 11, 13)));

        var sut = new AutomationImageProbe(pipeline.Object);

        var result = await sut.ProbeAsync(
            CreateHaystack(),
            20,
            30,
            null,
            new AutomationImageProbeOptions(0.1, 500),
            AutomationVisionAlgorithmKind.TextRegion,
            CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(31, result.MatchScreenXPx);
        Assert.Equal(43, result.MatchScreenYPx);
    }

    [Fact]
    public async Task ProbeAsync_TemplateMatch_OnMiss_PassesRawCorrelation()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.TemplateMatch,
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(false, 0, 0, 0, 0, 0, 0, 0, 0, 0.41)));

        var sut = new AutomationImageProbe(pipeline.Object);
        var haystack = CreateHaystack();
        var needle = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[64], 16);
        needle.Freeze();

        var result = await sut.ProbeAsync(
            haystack,
            0,
            0,
            needle,
            new AutomationImageProbeOptions(0.1, 500),
            AutomationVisionAlgorithmKind.TemplateMatch,
            CancellationToken.None);

        Assert.False(result.Matched);
        Assert.Equal(0.41, result.BestTemplateCorrelation);
    }

    [Fact]
    public async Task ProbeAsync_TemplateMatch_OffsetsScreenByHalfNeedleSize()
    {
        var pipeline = new Mock<IAutomationVisionPipeline>();
        pipeline
            .Setup(p => p.ProcessAsync(
                AutomationVisionAlgorithmKind.TemplateMatch,
                It.IsAny<AutomationVisionFrame>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationVisionResult(true, 10, 20, 1, 0.9)));

        var sut = new AutomationImageProbe(pipeline.Object);
        var haystack = CreateHaystack();
        var needle = BitmapSource.Create(10, 8, 96, 96, PixelFormats.Bgra32, null, new byte[320], 40);
        needle.Freeze();

        var result = await sut.ProbeAsync(
            haystack,
            1000,
            2000,
            needle,
            new AutomationImageProbeOptions(0.1, 500),
            AutomationVisionAlgorithmKind.TemplateMatch,
            CancellationToken.None);

        Assert.True(result.Matched);
        Assert.Equal(1000 + 10 + 5, result.MatchScreenXPx);
        Assert.Equal(2000 + 20 + 4, result.MatchScreenYPx);
    }

    private static BitmapSource CreateHaystack()
    {
        var bitmap = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, new byte[256], 32);
        if (bitmap.CanFreeze)
            bitmap.Freeze();

        return bitmap;
    }
}
