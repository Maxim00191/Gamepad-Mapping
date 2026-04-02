using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using Moq;
using System.Collections.ObjectModel;

namespace GamepadMapping.Tests.ViewModels;

public class ProcessTargetPanelViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly MainViewModel _mainViewModel;

    public ProcessTargetPanelViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
        _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        _mainViewModel = new MainViewModel(
            profileService: _profileServiceMock.Object,
            gamepadReader: new Mock<IGamepadReader>().Object,
            processTargetService: new Mock<IProcessTargetService>().Object,
            keyboardCaptureService: new Mock<IKeyboardCaptureService>().Object,
            elevationHandler: new Mock<IElevationHandler>().Object,
            appStatusMonitor: new Mock<IAppStatusMonitor>().Object,
            mappingEngine: new Mock<IMappingEngine>().Object,
            settingsService: _settingsServiceMock.Object
        );
    }

    [Fact]
    public void TemplateTargetProcessName_SyncsWithMainViewModel()
    {
        var vm = _mainViewModel.ProcessTargetPanel;
        
        vm.TemplateTargetProcessName = "notepad.exe";
        
        Assert.Equal("notepad.exe", _mainViewModel.TemplateTargetProcessName);
    }

    [Fact]
    public void TargetStatusText_UpdatesWhenMainViewModelChanges()
    {
        var vm = _mainViewModel.ProcessTargetPanel;
        
        _mainViewModel.TargetStatusText = "Running";
        
        Assert.Equal("Running", vm.TargetStatusText);
    }

    [Fact]
    public void TargetState_UpdatesWhenMainViewModelChanges()
    {
        var vm = _mainViewModel.ProcessTargetPanel;
        
        _mainViewModel.TargetState = AppTargetingState.Connected;
        
        Assert.Equal(AppTargetingState.Connected, vm.TargetState);
    }
}
