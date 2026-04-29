#nullable enable

using GamepadMapperGUI.Models.Automation;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class AutomationVisionAlgorithmStorageTests
{
    [Theory]
    [InlineData("template", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("TEMPLATE", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("color_threshold", AutomationVisionAlgorithmKind.ColorThreshold)]
    [InlineData("contour", AutomationVisionAlgorithmKind.Contour)]
    [InlineData("opencv_template", AutomationVisionAlgorithmKind.OpenCvTemplateMatch)]
    [InlineData("yolo_onnx", AutomationVisionAlgorithmKind.YoloOnnx)]
    [InlineData("text_region", AutomationVisionAlgorithmKind.TextRegion)]
    [InlineData("ocr_phrase", AutomationVisionAlgorithmKind.OcrPhraseMatch)]
    [InlineData("unknown", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("", AutomationVisionAlgorithmKind.TemplateMatch)]
    public void ParseKind_maps_storage_strings(string raw, AutomationVisionAlgorithmKind expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmStorage.ParseKind(raw));

    [Fact]
    public void ParseKind_null_returns_template()
    {
        Assert.Equal(AutomationVisionAlgorithmKind.TemplateMatch, AutomationVisionAlgorithmStorage.ParseKind(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseFindImageAlgorithmKind_missing_defaults_to_yolo(string? raw) =>
        Assert.Equal(AutomationVisionAlgorithmKind.YoloOnnx, AutomationVisionAlgorithmStorage.ParseFindImageAlgorithmKind(raw));

    [Fact]
    public void ParseFindImageAlgorithmKind_explicit_storage_preserved() =>
        Assert.Equal(
            AutomationVisionAlgorithmKind.TemplateMatch,
            AutomationVisionAlgorithmStorage.ParseFindImageAlgorithmKind("template"));

    [Fact]
    public void ToStorageValue_round_trips_enum_values()
    {
        Assert.Equal(
            AutomationVisionAlgorithmStorage.TemplateMatch,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.TemplateMatch));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.ColorThreshold,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.ColorThreshold));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.Contour,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.Contour));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.OpenCvTemplateMatch,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.OpenCvTemplateMatch));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.YoloOnnx,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.YoloOnnx));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.TextRegion,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.TextRegion));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.OcrPhraseMatch,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.OcrPhraseMatch));
    }
}
