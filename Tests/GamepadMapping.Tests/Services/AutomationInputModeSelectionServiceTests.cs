#nullable enable

using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using Moq;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationInputModeSelectionServiceTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Win32", InputEmulationApiIds.Win32)]
    [InlineData("win32", InputEmulationApiIds.Win32)]
    [InlineData("InputInjection", InputEmulationApiIds.InputInjection)]
    [InlineData("inputinjection", InputEmulationApiIds.InputInjection)]
    [InlineData("WinInjection", InputEmulationApiIds.InputInjection)]
    [InlineData("custom-mode", "custom-mode")]
    public void NormalizeModeId_ReturnsExpectedValue(string? raw, string expected)
    {
        var normalized = AutomationInputModeCatalog.NormalizeModeId(raw);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void BuildInputModePickerDisplayText_UsesGlobalLabelForEmptyInput()
    {
        var sut = new AutomationInputModeSelectionService(new Mock<IItemSelectionDialogService>().Object);

        var text = sut.BuildInputModePickerDisplayText("  ");

        Assert.Equal("AutomationInputModePicker_Global", text);
    }

    [Fact]
    public void PickInputModeId_NormalizesInitialSelectionBeforeDialog()
    {
        var dialog = new Mock<IItemSelectionDialogService>(MockBehavior.Strict);
        dialog
            .Setup(d => d.Select(
                null,
                "AutomationInputModePicker_DialogTitle",
                "AutomationInputModePicker_SearchPlaceholder",
                It.IsAny<IReadOnlyList<Gamepad_Mapping.Models.State.SelectionDialogItem>>(),
                InputEmulationApiIds.InputInjection))
            .Returns(InputEmulationApiIds.InputInjection);
        var sut = new AutomationInputModeSelectionService(dialog.Object);

        var selected = sut.PickInputModeId(null, "WinInjection");

        Assert.Equal(InputEmulationApiIds.InputInjection, selected);
        dialog.VerifyAll();
    }
}
