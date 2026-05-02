#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapping.Tests.Models;

public sealed class AutomationVisionAlgorithmCatalogTests
{
    [Fact]
    public void FindImageAlgorithmChoiceOptions_maps_one_entry_per_storage_constant()
    {
        var options = AutomationVisionAlgorithmCatalog.FindImageAlgorithmChoiceOptions();

        Assert.Equal(7, options.Count);

        Assert.Equal(
            AutomationVisionAlgorithmStorage.YoloOnnx,
            options[0].StoredValue);
        Assert.Equal(
            AutomationVisionAlgorithmKind.YoloOnnx,
            AutomationVisionAlgorithmStorage.ParseKind(options[0].StoredValue));

        Assert.Equal(
            AutomationVisionAlgorithmStorage.Contour,
            options[^1].StoredValue);
        Assert.Equal(
            AutomationVisionAlgorithmKind.Contour,
            AutomationVisionAlgorithmStorage.ParseKind(options[^1].StoredValue));
        Assert.Contains(options, option =>
            option.StoredValue == AutomationVisionAlgorithmStorage.TextRegion &&
            option.LabelResourceKey == "AutomationVisionAlgorithm_Option_TextRegion");
        Assert.Contains(options, option =>
            option.StoredValue == AutomationVisionAlgorithmStorage.OcrPhraseMatch &&
            option.LabelResourceKey == "AutomationVisionAlgorithm_Option_OcrPhraseMatch");
    }
}
