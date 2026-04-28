#nullable enable

using Gamepad_Mapping.Models.State;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using Moq;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationVisionAlgorithmSelectionServiceTests
{
    [Theory]
    [InlineData(null, "AutomationVisionAlgorithm_Option_YoloOnnx")]
    [InlineData("", "AutomationVisionAlgorithm_Option_YoloOnnx")]
    [InlineData("template", "AutomationVisionAlgorithm_Option_TemplateMatch")]
    [InlineData("opencv_template", "AutomationVisionAlgorithm_Option_OpenCvTemplateMatch")]
    [InlineData("text_region", "AutomationVisionAlgorithm_Option_TextRegion")]
    public void BuildFindImageAlgorithmPickerDisplayText_UsesNormalizedLocalizedLabel(
        string? raw,
        string expected)
    {
        var sut = new AutomationVisionAlgorithmSelectionService(new Mock<IItemSelectionDialogService>().Object);

        var text = sut.BuildFindImageAlgorithmPickerDisplayText(raw);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void PickFindImageAlgorithm_NormalizesInitialSelectionBeforeDialog()
    {
        var dialog = new Mock<IItemSelectionDialogService>(MockBehavior.Strict);
        dialog
            .Setup(d => d.Select(
                null,
                "AutomationVisionAlgorithmPicker_DialogTitle",
                "AutomationVisionAlgorithmPicker_SearchPlaceholder",
                It.Is<IReadOnlyList<SelectionDialogItem>>(items =>
                    items.Count == 6 &&
                    items.Any(item => item.Key == AutomationVisionAlgorithmStorage.YoloOnnx) &&
                    items.Any(item => item.Key == AutomationVisionAlgorithmStorage.OpenCvTemplateMatch) &&
                    items.Any(item => item.Key == AutomationVisionAlgorithmStorage.TextRegion)),
                AutomationVisionAlgorithmStorage.YoloOnnx))
            .Returns(AutomationVisionAlgorithmStorage.OpenCvTemplateMatch);
        var sut = new AutomationVisionAlgorithmSelectionService(dialog.Object);

        var selected = sut.PickFindImageAlgorithm(null, null);

        Assert.Equal(AutomationVisionAlgorithmStorage.OpenCvTemplateMatch, selected);
        dialog.VerifyAll();
    }
}
