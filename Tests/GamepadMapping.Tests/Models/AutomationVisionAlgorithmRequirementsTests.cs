#nullable enable

using GamepadMapperGUI.Models.Automation;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class AutomationVisionAlgorithmRequirementsTests
{
    [Theory]
    [InlineData(AutomationVisionAlgorithmKind.TemplateMatch, true)]
    [InlineData(AutomationVisionAlgorithmKind.OpenCvTemplateMatch, true)]
    [InlineData(AutomationVisionAlgorithmKind.ColorThreshold, false)]
    [InlineData(AutomationVisionAlgorithmKind.YoloOnnx, false)]
    [InlineData(AutomationVisionAlgorithmKind.TextRegion, false)]
    [InlineData(AutomationVisionAlgorithmKind.OcrPhraseMatch, false)]
    public void RequiresNeedleImage_reflects_algorithm(AutomationVisionAlgorithmKind kind, bool expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmRequirements.RequiresNeedleImage(kind));

    [Theory]
    [InlineData(AutomationVisionAlgorithmKind.YoloOnnx, true)]
    [InlineData(AutomationVisionAlgorithmKind.TemplateMatch, false)]
    [InlineData(AutomationVisionAlgorithmKind.OcrPhraseMatch, false)]
    public void RequiresYoloOnnxModel_reflects_algorithm(AutomationVisionAlgorithmKind kind, bool expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmRequirements.RequiresYoloOnnxModel(kind));

    [Theory]
    [InlineData(AutomationVisionAlgorithmKind.ColorThreshold, true)]
    [InlineData(AutomationVisionAlgorithmKind.Contour, true)]
    [InlineData(AutomationVisionAlgorithmKind.TextRegion, false)]
    [InlineData(AutomationVisionAlgorithmKind.OcrPhraseMatch, false)]
    public void UsesColorDetectionOptions_reflects_algorithm(AutomationVisionAlgorithmKind kind, bool expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmRequirements.UsesColorDetectionOptions(kind));

    [Theory]
    [InlineData(AutomationVisionAlgorithmKind.TextRegion, true)]
    [InlineData(AutomationVisionAlgorithmKind.ColorThreshold, false)]
    [InlineData(AutomationVisionAlgorithmKind.OcrPhraseMatch, false)]
    public void UsesTextDetectionOptions_reflects_algorithm(AutomationVisionAlgorithmKind kind, bool expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmRequirements.UsesTextDetectionOptions(kind));

    [Theory]
    [InlineData(AutomationVisionAlgorithmKind.OcrPhraseMatch, true)]
    [InlineData(AutomationVisionAlgorithmKind.TemplateMatch, false)]
    public void RequiresOcrPhraseList_reflects_algorithm(AutomationVisionAlgorithmKind kind, bool expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmRequirements.RequiresOcrPhraseList(kind));
}
