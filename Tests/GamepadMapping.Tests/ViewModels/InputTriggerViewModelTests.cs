using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.ViewModels.Strategies;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Moq;
using System.Collections.ObjectModel;
using Xunit;

namespace GamepadMapping.Tests.ViewModels;

public class InputTriggerViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<IKeyboardCaptureService> _keyboardCaptureServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly MainViewModel _mainViewModel;

    public InputTriggerViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _keyboardCaptureServiceMock = new Mock<IKeyboardCaptureService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
        _keyboardCaptureServiceMock.Setup(k => k.KeyboardKeyCapturePrompt).Returns(string.Empty);
        _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        _mainViewModel = new MainViewModel(
            profileService: _profileServiceMock.Object,
            keyboardCaptureService: _keyboardCaptureServiceMock.Object,
            gamepadReader: new Mock<IGamepadReader>().Object,
            processTargetService: new Mock<IProcessTargetService>().Object,
            elevationHandler: new Mock<IElevationHandler>().Object,
            appStatusMonitor: new Mock<IAppStatusMonitor>().Object,
            mappingEngine: new Mock<IMappingEngine>().Object,
            settingsService: _settingsServiceMock.Object
        );
    }

    [Fact]
    public void SyncFrom_LeftThumbstick_LoadsKindAndValue()
    {
        var sut = _mainViewModel.MappingEditorPanel.InputTrigger;
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "RIGHT" }
        };

        sut.SyncFrom(mapping);

        Assert.Equal(GamepadBindingType.LeftThumbstick, sut.EditSourceKind);
        Assert.Equal("RIGHT", sut.EditThumbstickFromValue);
    }

    [Fact]
    public void ApplyTo_LeftThumbstick_WritesCanonicalValue()
    {
        var sut = _mainViewModel.MappingEditorPanel.InputTrigger;
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "UP" }
        };
        sut.SyncFrom(mapping);
        sut.EditThumbstickFromValue = "left";

        Assert.True(sut.ApplyTo(mapping));
        Assert.Equal(GamepadBindingType.LeftThumbstick, mapping.From.Type);
        Assert.Equal("LEFT", mapping.From.Value);
    }

    [Fact]
    public void ApplyTo_NativeLeftTrigger_WritesTypeAndValue()
    {
        var sut = _mainViewModel.MappingEditorPanel.InputTrigger;
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        sut.SyncFrom(mapping);
        sut.EditSourceKind = GamepadBindingType.LeftTrigger;
        sut.EditBindingFromButton = nameof(GamepadBindingType.LeftTrigger);

        Assert.True(sut.ApplyTo(mapping));
        Assert.Equal(GamepadBindingType.LeftTrigger, mapping.From.Type);
        Assert.Equal(nameof(GamepadBindingType.LeftTrigger), mapping.From.Value);
    }

    [Fact]
    public void SyncFrom_RightThumbstick_NormalizesValueCasing()
    {
        var sut = _mainViewModel.MappingEditorPanel.InputTrigger;
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "up" }
        };

        sut.SyncFrom(mapping);

        Assert.Equal("UP", sut.EditThumbstickFromValue);
    }
}
