#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationNodeInlineEditorSchemaServiceTests
{
    [Fact]
    public void GetDefinitions_IncludesCommonNodeMetadataBeforeTypeSpecificFields()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("perception.capture_screen");

        Assert.True(definitions.Count > 1);
        Assert.Equal(AutomationNodePropertyKeys.Description, definitions[0].PropertyKey);
        Assert.Contains(definitions, definition =>
            definition.PropertyKey == AutomationNodePropertyKeys.CaptureCacheRefNodeId);
    }

    [Fact]
    public void GetDefinitions_CaptureScreenCacheRef_UsesTextEditorWithPlaceholder()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("perception.capture_screen");
        var cacheRef = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.CaptureCacheRefNodeId);

        Assert.Equal(AutomationNodeInlineEditorKind.Text, cacheRef.Kind);
        Assert.Equal("AutomationInlineEditor_CaptureCacheRefNodeId", cacheRef.LabelResourceKey);
        Assert.Equal("AutomationInlineEditor_CaptureCacheRefNodeIdPlaceholder", cacheRef.PlaceholderResourceKey);
        Assert.Equal(string.Empty, cacheRef.DefaultTextValue);
    }

    [Fact]
    public void GetDefinitions_FindImageYoloPath_DefaultsToBundledRelativePath()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("perception.find_image");
        var yoloPath = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageYoloOnnxPath);

        Assert.Equal(AutomationYoloOnnxPaths.DefaultBundledModelRelativePath, yoloPath.DefaultTextValue);
    }

    [Fact]
    public void GetDefinitions_FindImageAlgorithm_UsesChoiceEditor()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("perception.find_image");
        var algorithm = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageAlgorithm);

        Assert.Equal(AutomationNodeInlineEditorKind.Choice, algorithm.Kind);
        Assert.Equal("AutomationInlineEditor_FindImageAlgorithm", algorithm.LabelResourceKey);
        Assert.Equal(AutomationVisionAlgorithmStorage.YoloOnnx, algorithm.DefaultTextValue);
        Assert.NotNull(algorithm.ChoiceOptions);
        Assert.Contains(algorithm.ChoiceOptions!, option => option.StoredValue == AutomationVisionAlgorithmStorage.YoloOnnx);
        Assert.Contains(algorithm.ChoiceOptions!, option => option.StoredValue == AutomationVisionAlgorithmStorage.OpenCvTemplateMatch);
        Assert.Contains(algorithm.ChoiceOptions!, option => option.StoredValue == AutomationVisionAlgorithmStorage.OcrPhraseMatch);
    }

    [Theory]
    [InlineData(
        "output.keyboard_key",
        AutomationNodePropertyKeys.KeyboardActionId,
        "AutomationInlineEditor_KeyboardActionPickerLabel")]
    [InlineData(
        "output.keyboard_key",
        AutomationNodePropertyKeys.InputEmulationApiId,
        "AutomationInlineEditor_InputModePickerLabel")]
    [InlineData(
        "output.mouse_click",
        AutomationNodePropertyKeys.MouseActionId,
        "AutomationInlineEditor_MouseActionPickerLabel")]
    [InlineData(
        "output.mouse_click",
        AutomationNodePropertyKeys.InputEmulationApiId,
        "AutomationInlineEditor_InputModePickerLabel")]
    public void GetDefinitions_ActionPickers_UsePickerLabelResourceKeys(
        string nodeTypeId,
        string propertyKey,
        string expectedLabelResourceKey)
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions(nodeTypeId);
        var picker = definitions.Single(d => d.PropertyKey == propertyKey);

        Assert.Equal(AutomationNodeInlineEditorKind.Action, picker.Kind);
        Assert.Equal(expectedLabelResourceKey, picker.LabelResourceKey);
    }

    [Fact]
    public void GetDefinitions_FindImageAlgorithmSpecificFields_HaveVisibilityConditions()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("perception.find_image");
        var needlePath = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageNeedlePath);
        var yoloPath = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageYoloOnnxPath);
        var yoloClass = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageYoloClassId);
        var colorTargetHex = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageColorTargetHex);
        var colorHueMin = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageColorHueMin);
        var colorMinimumArea = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageColorMinimumAreaPx);
        var textQuery = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageTextQuery);
        var textMinimumArea = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageTextMinimumRegionAreaPx);
        var ocrPhrases = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageOcrPhrases);
        var ocrCase = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageOcrCaseSensitive);
        var ocrMaxEdge = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageOcrMaxLongEdgePx);
        var tolerance = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageTolerance);
        var timeout = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageTimeoutMs);
        var confidence = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.FindImageConfidence);

        Assert.Equal(AutomationNodePropertyKeys.FindImageAlgorithm, needlePath.VisibleWhenPropertyKey);
        Assert.Contains(AutomationVisionAlgorithmStorage.YoloOnnx, needlePath.VisibleWhenPropertyValues!);
        Assert.Contains("", needlePath.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.TemplateMatch, needlePath.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.OpenCvTemplateMatch, needlePath.VisibleWhenPropertyValues!);

        Assert.Contains("", yoloPath.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.YoloOnnx, yoloPath.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.TemplateMatch, yoloPath.VisibleWhenPropertyValues!);

        Assert.Equal(yoloPath.VisibleWhenPropertyValues, yoloClass.VisibleWhenPropertyValues);
        Assert.Contains(AutomationVisionAlgorithmStorage.ColorThreshold, colorHueMin.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.Contour, colorMinimumArea.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.YoloOnnx, colorHueMin.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.ColorThreshold, colorTargetHex.VisibleWhenPropertyValues!);
        Assert.Equal(AutomationNodeInlineEditorKind.Text, colorTargetHex.Kind);
        Assert.Contains(AutomationVisionAlgorithmStorage.TextRegion, textMinimumArea.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.ColorThreshold, textMinimumArea.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.TextRegion, textQuery.VisibleWhenPropertyValues!);
        Assert.Equal(AutomationNodeInlineEditorKind.Text, textQuery.Kind);
        Assert.Contains(AutomationVisionAlgorithmStorage.OcrPhraseMatch, ocrPhrases.VisibleWhenPropertyValues!);
        Assert.Equal(AutomationNodeInlineEditorKind.MultilineText, ocrPhrases.Kind);
        Assert.Contains(AutomationVisionAlgorithmStorage.OcrPhraseMatch, ocrCase.VisibleWhenPropertyValues!);
        Assert.Equal(AutomationNodeInlineEditorKind.Boolean, ocrCase.Kind);
        Assert.Contains(AutomationVisionAlgorithmStorage.OcrPhraseMatch, ocrMaxEdge.VisibleWhenPropertyValues!);
        Assert.Equal(AutomationNodeInlineEditorKind.Integer, ocrMaxEdge.Kind);
        Assert.Contains(AutomationVisionAlgorithmStorage.OpenCvTemplateMatch, tolerance.VisibleWhenPropertyValues!);
        Assert.Contains(AutomationVisionAlgorithmStorage.OpenCvTemplateMatch, timeout.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.ColorThreshold, tolerance.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.Contour, timeout.VisibleWhenPropertyValues!);
        Assert.DoesNotContain(AutomationVisionAlgorithmStorage.TextRegion, timeout.VisibleWhenPropertyValues!);
        Assert.Equal(tolerance.VisibleWhenPropertyValues, confidence.VisibleWhenPropertyValues);
        Assert.Equal(AutomationNodeInlineEditorKind.Double, confidence.Kind);
    }

    [Fact]
    public void GetDefinitions_MathAdd_ExposesOperandFallbacks()
    {
        var service = new AutomationNodeInlineEditorSchemaService();
        var definitions = service.GetDefinitions("math.add");

        Assert.Contains(definitions, d => d.PropertyKey == AutomationNodePropertyKeys.MathLeft);
        Assert.Contains(definitions, d => d.PropertyKey == AutomationNodePropertyKeys.MathRight);
    }

    [Fact]
    public void GetDefinitions_LoopControl_UsesChoiceForMode()
    {
        var service = new AutomationNodeInlineEditorSchemaService();
        var definitions = service.GetDefinitions("logic.loop_control");
        var mode = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.LoopControlMode);

        Assert.Equal(AutomationNodeInlineEditorKind.Choice, mode.Kind);
        Assert.NotNull(mode.ChoiceOptions);
        Assert.Contains(mode.ChoiceOptions!, o => o.StoredValue == "break");
        Assert.Contains(mode.ChoiceOptions!, o => o.StoredValue == "continue");
    }

    [Fact]
    public void GetDefinitions_Loop_ExposesPacingProperties()
    {
        var service = new AutomationNodeInlineEditorSchemaService();
        var definitions = service.GetDefinitions("automation.loop");

        Assert.Contains(definitions, d => d.PropertyKey == AutomationNodePropertyKeys.LoopTargetIterationsPerSecond);
        Assert.Contains(definitions, d => d.PropertyKey == AutomationNodePropertyKeys.LoopInteriorSkipDocumentStepInterval);
        var target = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.LoopTargetIterationsPerSecond);
        Assert.Equal(AutomationNodeInlineEditorKind.Double, target.Kind);
        var skip = definitions.Single(d => d.PropertyKey == AutomationNodePropertyKeys.LoopInteriorSkipDocumentStepInterval);
        Assert.Equal(AutomationNodeInlineEditorKind.Boolean, skip.Kind);
    }

    [Fact]
    public void GetDefinitions_UnknownNodeStillSupportsMetadata()
    {
        var service = new AutomationNodeInlineEditorSchemaService();

        var definitions = service.GetDefinitions("custom.unknown");

        Assert.Collection(
            definitions,
            description =>
            {
                Assert.Equal(AutomationNodePropertyKeys.Description, description.PropertyKey);
                Assert.Equal(AutomationNodeInlineEditorKind.MultilineText, description.Kind);
            });
    }
}
