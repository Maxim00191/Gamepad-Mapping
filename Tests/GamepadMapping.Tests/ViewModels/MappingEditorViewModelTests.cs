using Gamepad_Mapping.ViewModels;
using Gamepad_Mapping.ViewModels.Strategies;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Storage;
using Moq;
using System.Collections.ObjectModel;
using GamepadMapperGUI.Core;
using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Core;
using System.Collections.Generic;

namespace GamepadMapping.Tests.ViewModels;

public class MappingEditorViewModelTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<IKeyboardCaptureService> _keyboardCaptureServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IItemSelectionDialogService> _itemSelectionDialogServiceMock;
    private readonly MainViewModel _mainViewModel;

    public MappingEditorViewModelTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _keyboardCaptureServiceMock = new Mock<IKeyboardCaptureService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _itemSelectionDialogServiceMock = new Mock<IItemSelectionDialogService>();
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
            settingsService: _settingsServiceMock.Object,
            itemSelectionDialogService: _itemSelectionDialogServiceMock.Object,
            profileDomainService: new ProfileDomainService());

        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;
    }

    [Fact]
    public void ChangingMainViewModelSelectedMapping_UpdatesEditorWithoutManualSync()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var a = new MappingEntry
        {
            Description = "RowA",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        var b = new MappingEntry
        {
            Description = "RowB",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" }
        };
        _mainViewModel.Mappings.Add(a);
        _mainViewModel.Mappings.Add(b);

        _mainViewModel.SelectedMapping = a;
        Assert.Equal("RowA", vm.EditBindingDescriptionPrimary);
        Assert.Equal("A", vm.InputTrigger.EditBindingFromButton);

        _mainViewModel.SelectedMapping = b;
        Assert.Equal("RowB", vm.EditBindingDescriptionPrimary);
        Assert.Equal("B", vm.InputTrigger.EditBindingFromButton);
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
        Assert.Equal("Jump", vm.EditBindingDescriptionPrimary);
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

        _keyboardCaptureServiceMock.Verify(k => k.BeginCapture(It.IsAny<string>(), It.IsAny<Action<Key>>()), Times.Once());
    }

    [Fact]
    public void UpdateSelectedBinding_UpdatesEntry()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        _mainViewModel.Mappings.Add(entry);
        _mainViewModel.SelectedMapping = entry;
        vm.SyncFromSelection(entry);

        if (vm.CurrentActionEditor is KeyboardActionEditorViewModel k) k.KeyboardKey = "Space";
        vm.UpdateSelectedBindingCommand.Execute(null);

        Assert.Equal("Space", entry.KeyboardKey);
    }

    [Fact]
    public void VisualSelection_UpdatesSharedMappingWorkspaceSelection()
    {
        var a = new MappingEntry
        {
            Description = "RowA",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        var b = new MappingEntry
        {
            Description = "RowB",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" }
        };
        _mainViewModel.Mappings.Add(a);
        _mainViewModel.Mappings.Add(b);
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;
        _mainViewModel.SelectMappingFromScope(a, GamepadMapperGUI.Models.State.WorkspaceSelectionScope.Mappings);

        _mainViewModel.VisualEditorPanel.ControllerVisual.SelectedElementName = "B";

        Assert.Null(_mainViewModel.SelectedMapping);
        Assert.Equal(ProfileRightPanelSurface.None, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void AddMappingCommand_CreatesUndoEntry_ForCreateModeWorkspaceState()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var template = new TemplateOption { ProfileId = "test", TemplateGroupId = "group", DisplayName = "Test" };
        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(template))
            .Returns(new GameProfileTemplate { ProfileId = "test", DisplayName = "Test", Mappings = [] });
        _mainViewModel.SelectedTemplate = template;

        vm.AddMappingCommand.Execute(null);

        Assert.True(vm.IsCreatingNewMapping);
        Assert.True(_mainViewModel.ActiveMappingListEditor.History.CanUndo);

        _mainViewModel.ActiveMappingListEditor.History.Undo();

        Assert.False(vm.IsCreatingNewMapping);
    }

    [Fact]
    public void SelectedActionType_ChangesEditor()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        
        vm.SelectedActionType = MappingActionType.ItemCycle;
        
        Assert.IsType<ItemCycleActionEditorViewModel>(vm.CurrentActionEditor);
        Assert.False(vm.EditKeyboardAndHoldSectionsEnabled);
    }

    [Fact]
    public void PickMappingActionIdCommand_AssignsSelectedCatalogActionId()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        _mainViewModel.KeyboardActions.Add(new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" });

        _itemSelectionDialogServiceMock
            .Setup(s => s.Select(It.IsAny<System.Windows.Window?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Gamepad_Mapping.Models.State.SelectionDialogItem>>(), It.IsAny<string?>()))
            .Returns("jump");

        vm.PickMappingActionIdCommand.Execute(entry);

        Assert.Equal("jump", entry.ActionId);
    }

    [Fact]
    public void RefreshStatusDiagnostics_WhenActionIsBoundMultipleTimes_ShowsDuplicateBanner()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        _mainViewModel.KeyboardActions.Add(new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" });
        _mainViewModel.Mappings.Add(new MappingEntry
        {
            ActionId = "jump",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LB+A" }
        });
        _mainViewModel.Mappings.Add(new MappingEntry
        {
            ActionId = "jump",
            From = new GamepadBinding { Type = GamepadBindingType.RightTrigger, Value = nameof(GamepadBindingType.RightTrigger) }
        });

        vm.RefreshStatusDiagnostics();

        Assert.True(vm.HasDuplicateActionIds);
        Assert.False(string.IsNullOrWhiteSpace(vm.DuplicateActionIdsHint));
        Assert.False(string.IsNullOrWhiteSpace(vm.DuplicateActionIdsTooltip));
    }

    [Fact]
    public void RefreshStatusDiagnostics_WhenActionIsRepeatedOnSameSource_HidesDuplicateBanner()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        _mainViewModel.KeyboardActions.Add(new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" });
        _mainViewModel.Mappings.Add(new MappingEntry
        {
            ActionId = "jump",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        });
        _mainViewModel.Mappings.Add(new MappingEntry
        {
            ActionId = "jump",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        });

        vm.RefreshStatusDiagnostics();

        Assert.False(vm.HasDuplicateActionIds);
        Assert.Equal(string.Empty, vm.DuplicateActionIdsHint);
        Assert.Equal(string.Empty, vm.DuplicateActionIdsTooltip);
    }

    [Fact]
    public void RefreshStatusDiagnostics_WhenActionIsBoundOnce_HidesDuplicateBanner()
    {
        var vm = _mainViewModel.MappingEditorPanel;
        _mainViewModel.KeyboardActions.Add(new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" });
        _mainViewModel.Mappings.Add(new MappingEntry
        {
            ActionId = "jump",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        });

        vm.RefreshStatusDiagnostics();

        Assert.False(vm.HasDuplicateActionIds);
        Assert.Equal(string.Empty, vm.DuplicateActionIdsHint);
        Assert.Equal(string.Empty, vm.DuplicateActionIdsTooltip);
    }

    [Fact]
    public void CatalogResetSelection_DoesNotChangeActiveWorkspaceScope()
    {
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;

        _mainViewModel.CatalogPanel.ResetSelection();

        Assert.Equal(
            GamepadMapperGUI.Models.State.WorkspaceSelectionScope.Mappings,
            _mainViewModel.ActiveWorkspaceSelectionScope);
    }

    [Fact]
    public void OffTabKeyboardSelection_DoesNotStealMappingsRightPanel()
    {
        var mapping = new MappingEntry
        {
            Description = "Mapped",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        _mainViewModel.Mappings.Add(mapping);
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;
        _mainViewModel.SelectMappingFromScope(mapping, GamepadMapperGUI.Models.State.WorkspaceSelectionScope.Mappings);

        _mainViewModel.KeyboardActions.Add(new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" });
        _mainViewModel.SelectKeyboardActionFromScope(
            _mainViewModel.KeyboardActions[0],
            GamepadMapperGUI.Models.State.WorkspaceSelectionScope.KeyboardCatalog);

        Assert.Equal(
            GamepadMapperGUI.Models.State.WorkspaceSelectionScope.Mappings,
            _mainViewModel.ActiveWorkspaceSelectionScope);
        Assert.Equal(ProfileRightPanelSurface.Mapping, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void KeyboardTabSelection_ShowsKeyboardRightPanel()
    {
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions;
        var action = new KeyboardActionDefinition { Id = "jump", KeyboardKey = "Space" };
        _mainViewModel.KeyboardActions.Add(action);

        _mainViewModel.SelectKeyboardActionFromScope(
            action,
            GamepadMapperGUI.Models.State.WorkspaceSelectionScope.KeyboardCatalog);

        Assert.Equal(ProfileRightPanelSurface.KeyboardAction, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void ActiveWorkspaceRuleClipboardKind_MatchesWorkspaceTab()
    {
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.VisualEditor;
        Assert.Equal(ProfileRuleClipboardKind.Mapping, _mainViewModel.ActiveWorkspaceRuleClipboardKind);

        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;
        Assert.Equal(ProfileRuleClipboardKind.Mapping, _mainViewModel.ActiveWorkspaceRuleClipboardKind);

        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions;
        Assert.Equal(ProfileRuleClipboardKind.KeyboardAction, _mainViewModel.ActiveWorkspaceRuleClipboardKind);

        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus;
        Assert.Equal(ProfileRuleClipboardKind.RadialMenu, _mainViewModel.ActiveWorkspaceRuleClipboardKind);

        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Community;
        Assert.Null(_mainViewModel.ActiveWorkspaceRuleClipboardKind);
    }

    [Fact]
    public void TemplateSwitch_OnMappingsTab_RehydratesSelectionAndMappingEditor()
    {
        var templateA = new TemplateOption { ProfileId = "a", TemplateGroupId = "g", DisplayName = "Template A" };
        var templateB = new TemplateOption { ProfileId = "b", TemplateGroupId = "g", DisplayName = "Template B" };
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;

        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "a")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "a",
                DisplayName = "Template A",
                Mappings =
                [
                    new MappingEntry
                    {
                        Description = "From A",
                        From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
                    }
                ]
            });
        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "b")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "b",
                DisplayName = "Template B",
                Mappings =
                [
                    new MappingEntry
                    {
                        Description = "From B",
                        From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" }
                    }
                ]
            });

        _mainViewModel.SelectedTemplate = templateA;
        _mainViewModel.SelectedTemplate = templateB;

        Assert.NotNull(_mainViewModel.SelectedMapping);
        Assert.Equal("From B", _mainViewModel.MappingEditorPanel.EditBindingDescriptionPrimary);
        Assert.Equal("B", _mainViewModel.MappingEditorPanel.InputTrigger.EditBindingFromButton);
        Assert.Equal(ProfileRightPanelSurface.Mapping, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void ResetTo_SameReference_ReplaysSelectionChangedAndRefreshesEditor()
    {
        var mapping = new MappingEntry
        {
            Description = "Mapped",
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
        };
        _mainViewModel.Mappings.Add(mapping);
        _mainViewModel.MappingSelection.ResetTo(mapping);
        Assert.Equal("Mapped", _mainViewModel.MappingEditorPanel.EditBindingDescriptionPrimary);

        _mainViewModel.MappingSelection.ResetTo(null);
        _mainViewModel.MappingSelection.ResetTo(mapping);

        Assert.Same(mapping, _mainViewModel.SelectedMapping);
        Assert.Equal("Mapped", _mainViewModel.MappingEditorPanel.EditBindingDescriptionPrimary);
        Assert.Equal(ProfileRightPanelSurface.Mapping, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void TemplateSwitch_OnKeyboardTab_SelectsFirstKeyboardAction()
    {
        var templateA = new TemplateOption { ProfileId = "a", TemplateGroupId = "g", DisplayName = "Template A" };
        var templateB = new TemplateOption { ProfileId = "b", TemplateGroupId = "g", DisplayName = "Template B" };
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.KeyboardActions;

        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "a")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "a",
                DisplayName = "Template A",
                Mappings = [],
                KeyboardActions = [new KeyboardActionDefinition { Id = "act-a", KeyboardKey = "Space", Description = "A" }]
            });
        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "b")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "b",
                DisplayName = "Template B",
                Mappings = [],
                KeyboardActions = [new KeyboardActionDefinition { Id = "act-b", KeyboardKey = "Enter", Description = "B" }]
            });

        _mainViewModel.SelectedTemplate = templateA;
        _mainViewModel.SelectedTemplate = templateB;

        Assert.NotNull(_mainViewModel.SelectedKeyboardAction);
        Assert.Equal("act-b", _mainViewModel.SelectedKeyboardAction?.Id);
        Assert.Equal(ProfileRightPanelSurface.KeyboardAction, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void TemplateSwitch_OnRadialTab_SelectsFirstRadialMenu()
    {
        var templateA = new TemplateOption { ProfileId = "a", TemplateGroupId = "g", DisplayName = "Template A" };
        var templateB = new TemplateOption { ProfileId = "b", TemplateGroupId = "g", DisplayName = "Template B" };
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.RadialMenus;

        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "a")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "a",
                DisplayName = "Template A",
                Mappings = [],
                RadialMenus = [new RadialMenuDefinition { Id = "radial-a", DisplayName = "A", Items = new ObservableCollection<RadialMenuItem>() }]
            });
        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "b")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "b",
                DisplayName = "Template B",
                Mappings = [],
                RadialMenus = [new RadialMenuDefinition { Id = "radial-b", DisplayName = "B", Items = new ObservableCollection<RadialMenuItem>() }]
            });

        _mainViewModel.SelectedTemplate = templateA;
        _mainViewModel.SelectedTemplate = templateB;

        Assert.NotNull(_mainViewModel.SelectedRadialMenu);
        Assert.Equal("radial-b", _mainViewModel.SelectedRadialMenu?.Id);
        Assert.Equal(ProfileRightPanelSurface.RadialMenu, _mainViewModel.RightPanelSurface);
    }

    [Fact]
    public void TemplateSwitch_ClearsWorkspaceSessionState_AndRehydratesSelection()
    {
        var templateA = new TemplateOption { ProfileId = "a", TemplateGroupId = "g", DisplayName = "Template A" };
        var templateB = new TemplateOption { ProfileId = "b", TemplateGroupId = "g", DisplayName = "Template B" };
        _mainViewModel.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;

        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "a")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "a",
                DisplayName = "Template A",
                Mappings =
                [
                    new MappingEntry
                    {
                        Description = "From A",
                        From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
                    }
                ]
            });
        _profileServiceMock
            .Setup(p => p.LoadSelectedTemplate(It.Is<TemplateOption>(t => t.ProfileId == "b")))
            .Returns(new GameProfileTemplate
            {
                ProfileId = "b",
                DisplayName = "Template B",
                Mappings =
                [
                    new MappingEntry
                    {
                        Description = "From B",
                        From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" }
                    }
                ]
            });

        _mainViewModel.SelectedTemplate = templateA;
        _mainViewModel.MappingEditorPanel.AddMappingCommand.Execute(null);
        Assert.True(_mainViewModel.MappingEditorPanel.IsCreatingNewMapping);
        Assert.True(_mainViewModel.ActiveMappingListEditor.History.CanUndo);

        _mainViewModel.SelectedTemplate = templateB;

        Assert.False(_mainViewModel.MappingEditorPanel.IsCreatingNewMapping);
        Assert.False(_mainViewModel.ActiveMappingListEditor.History.CanUndo);
        Assert.False(_mainViewModel.ActiveMappingListEditor.History.CanRedo);
        Assert.NotNull(_mainViewModel.SelectedMapping);
        Assert.Equal("From B", _mainViewModel.MappingEditorPanel.EditBindingDescriptionPrimary);
        Assert.Equal("B", _mainViewModel.MappingEditorPanel.InputTrigger.EditBindingFromButton);
        Assert.Equal(ProfileRightPanelSurface.Mapping, _mainViewModel.RightPanelSurface);
    }
}

