using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.ViewModels.Strategies;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using Moq;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Core;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;

namespace GamepadMapping.Tests.ViewModels;

public class MappingEditorViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<IKeyboardCaptureService> _keyboardCaptureServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly MainViewModel _mainViewModel;

    public MappingEditorViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _keyboardCaptureServiceMock = new Mock<IKeyboardCaptureService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
        _keyboardCaptureServiceMock.Setup(k => k.KeyboardKeyCapturePrompt).Returns("Prompt");
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
    public void SyncFromSelection_PopulatesProperties()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
            KeyboardKey = "Space",
            Trigger = TriggerMoment.Tap,
            Description = "Jump"
        };

        vm.SyncFromSelection(entry);

        Assert.Equal("A", vm.InputTrigger.EditBindingFromButton);
        Assert.Equal("Space", (vm.CurrentActionEditor as KeyboardActionEditorViewModel)?.KeyboardKey);
        Assert.Equal(TriggerMoment.Tap, vm.EditBindingTrigger);
        Assert.Equal("Jump", vm.EditBindingDescription);
    }

    [Fact]
    public void BeginCreateNewMapping_ResetsProperties()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        if (vm.CurrentActionEditor is KeyboardActionEditorViewModel k) k.KeyboardKey = "X";
        
        vm.AddMappingCommand.Execute(null);

        Assert.True(vm.IsCreatingNewMapping);
        Assert.Equal(string.Empty, (vm.CurrentActionEditor as KeyboardActionEditorViewModel)?.KeyboardKey);
        Assert.Null(_mainViewModel.SelectedMapping);
    }

    [Fact]
    public void RecordKeyboardKey_CallsCaptureService()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var entry = new MappingEntry();
        _mainViewModel.SelectedMapping = entry;

        vm.RecordKeyboardKeyCommand.Execute(null);

        _keyboardCaptureServiceMock.Verify(k => k.BeginCapture(It.IsAny<string>(), It.IsAny<Action<Key>>()), Times.Once);
    }

    [Fact]
    public void UpdateSelectedBinding_UpdatesEntry()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        _mainViewModel.SelectedMapping = entry;
        vm.SyncFromSelection(entry);

        if (vm.CurrentActionEditor is KeyboardActionEditorViewModel k) k.KeyboardKey = "Space";
        vm.UpdateSelectedBindingCommand.Execute(null);

        Assert.Equal("Space", entry.KeyboardKey);
    }

    [Fact]
    public void SelectedActionType_ChangesEditor()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        
        vm.SelectedActionType = MappingActionType.ItemCycle;
        
        Assert.IsType<ItemCycleActionEditorViewModel>(vm.CurrentActionEditor);
        Assert.False(vm.EditKeyboardAndHoldSectionsEnabled);
    }
}

