using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using Moq;
using System.Collections.ObjectModel;

namespace GamepadMapping.Tests.ViewModels;

public class ProcessTargetPanelViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IAppStatusMonitor> _appStatusMonitorMock;
    private readonly MainViewModel _mainViewModel;

    public ProcessTargetPanelViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _appStatusMonitorMock = new Mock<IAppStatusMonitor>();
        _profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
        _settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        _mainViewModel = new MainViewModel(
            profileService: _profileServiceMock.Object,
            gamepadReader: new Mock<IGamepadReader>().Object,
            processTargetService: new Mock<IProcessTargetService>().Object,
            keyboardCaptureService: new Mock<IKeyboardCaptureService>().Object,
            elevationHandler: new Mock<IElevationHandler>().Object,
            appStatusMonitor: _appStatusMonitorMock.Object,
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

        _appStatusMonitorMock.Raise(
            m => m.StatusChanged += null,
            _appStatusMonitorMock.Object,
            new AppStatusChangedEventArgs(AppTargetingState.NoTargetSelected, "Running"));

        Assert.Equal("Running", vm.TargetStatusText);
    }

    [Fact]
    public void TargetState_UpdatesWhenMainViewModelChanges()
    {
        var vm = _mainViewModel.ProcessTargetPanel;

        _appStatusMonitorMock.Raise(
            m => m.StatusChanged += null,
            _appStatusMonitorMock.Object,
            new AppStatusChangedEventArgs(AppTargetingState.Connected, "status"));

        Assert.Equal(AppTargetingState.Connected, vm.TargetState);
    }

    [Fact]
    public void ShouldHighlightTargetProcessRefresh_TrueWhenResolvedPidIsZero()
    {
        var vm = _mainViewModel.ProcessTargetPanel;

        _mainViewModel.SelectedTargetProcess = new ProcessInfo
        {
            ProcessId = 0,
            ProcessName = "SomeGame"
        };

        Assert.True(vm.ShouldHighlightTargetProcessRefresh);
    }

    [Fact]
    public void ShouldHighlightTargetProcessRefresh_FalseWhenResolvedPidIsPositive()
    {
        var vm = _mainViewModel.ProcessTargetPanel;

        _mainViewModel.SelectedTargetProcess = new ProcessInfo
        {
            ProcessId = 1234,
            ProcessName = "SomeGame"
        };

        Assert.False(vm.ShouldHighlightTargetProcessRefresh);
    }

    [Fact]
    public void RefreshDeclaredProcessTargetCommand_IsExposedFromPanel()
    {
        var vm = _mainViewModel.ProcessTargetPanel;

        Assert.NotNull(vm.RefreshDeclaredProcessTargetCommand);
        Assert.True(vm.RefreshDeclaredProcessTargetCommand.CanExecute(null));
    }
}

