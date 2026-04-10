using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using Moq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GamepadMapperGUI.Core;

namespace GamepadMapping.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<IGamepadReader> _gamepadReaderMock;
    private readonly Mock<IProcessTargetService> _processTargetServiceMock;
    private readonly Mock<IKeyboardCaptureService> _keyboardCaptureServiceMock;
    private readonly Mock<IElevationHandler> _elevationHandlerMock;
    private readonly Mock<IAppStatusMonitor> _appStatusMonitorMock;
    private readonly Mock<IMappingEngine> _mappingEngineMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;

    public MainViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _gamepadReaderMock = new Mock<IGamepadReader>();
        _processTargetServiceMock = new Mock<IProcessTargetService>();
        _keyboardCaptureServiceMock = new Mock<IKeyboardCaptureService>();
        _elevationHandlerMock = new Mock<IElevationHandler>();
        _appStatusMonitorMock = new Mock<IAppStatusMonitor>();
        _mappingEngineMock = new Mock<IMappingEngine>();
        _settingsServiceMock = new Mock<ISettingsService>();

        // Default setups to avoid null issues during construction
        _profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
        _keyboardCaptureServiceMock.Setup(k => k.KeyboardKeyCapturePrompt).Returns("Prompt");
        _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());
    }

    [Fact]
    public void Constructor_InitializesSubViewModels()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.ProfileTemplatePanel);
        Assert.NotNull(vm.NewBindingPanel);
        Assert.NotNull(vm.MappingEditorPanel);
        Assert.NotNull(vm.CatalogPanel);
        Assert.NotNull(vm.GamepadMonitorPanel);
        Assert.NotNull(vm.ProcessTargetPanel);
    }

    [Fact]
    public void StartGamepad_CallsGamepadReaderStart()
    {
        var vm = CreateViewModel();
        vm.StartGamepadCommand.Execute(null);

        _gamepadReaderMock.Verify(r => r.Start(), Times.Once);
        Assert.True(vm.IsGamepadRunning);
    }

    [Fact]
    public void StopGamepad_CallsGamepadReaderStopAndReleasesOutputs()
    {
        var vm = CreateViewModel();
        vm.StartGamepadCommand.Execute(null); // Ensure it's running
        vm.StopGamepadCommand.Execute(null);

        _gamepadReaderMock.Verify(r => r.Stop(), Times.Once);
        _mappingEngineMock.Verify(e => e.ForceReleaseAllOutputs(), Times.Once);
        Assert.False(vm.IsGamepadRunning);
    }

    [Fact]
    public void SelectedTemplateChanged_LoadsTemplate()
    {
        var template = new TemplateOption { ProfileId = "test", TemplateGroupId = "Test Group", DisplayName = "Test Display" };
        var profile = new GameProfileTemplate { ProfileId = "test", DisplayName = "Test Display", Mappings = new List<MappingEntry>() };
        _profileServiceMock.Setup(p => p.LoadSelectedTemplate(template)).Returns(profile);

        var vm = CreateViewModel();
        vm.SelectedTemplate = template;

        _profileServiceMock.Verify(p => p.LoadSelectedTemplate(template), Times.AtLeastOnce);
        Assert.Equal("Test Display", vm.CurrentTemplateDisplayName);
    }

    [Fact]
    public void PropertyChanged_TriggersSubViewModelUpdates()
    {
        var vm = CreateViewModel();
        bool mappingEditorNotified = false;
        vm.MappingEditorPanel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MappingEditorViewModel.Mappings))
                mappingEditorNotified = true;
        };

        vm.Mappings.Add(new MappingEntry());

        Assert.True(mappingEditorNotified);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _profileServiceMock.Object,
            _gamepadReaderMock.Object,
            _processTargetServiceMock.Object,
            _keyboardCaptureServiceMock.Object,
            _elevationHandlerMock.Object,
            _appStatusMonitorMock.Object,
            _mappingEngineMock.Object,
            _settingsServiceMock.Object);
    }
}

