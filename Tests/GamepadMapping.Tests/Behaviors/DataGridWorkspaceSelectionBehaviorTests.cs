using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Gamepad_Mapping.Behaviors;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Storage;
using Moq;
using System.Collections.ObjectModel;

namespace GamepadMapping.Tests.Behaviors;

public class DataGridWorkspaceSelectionBehaviorTests
{
    [Fact]
    public void MappingsGrid_SelectionStillPropagatesToMainViewModel_AfterSimulatedTabUnloadReload()
    {
        RunSta(() =>
        {
            var profileServiceMock = new Mock<IProfileService>();
            profileServiceMock.Setup(p => p.AvailableTemplates).Returns(new ObservableCollection<TemplateOption>());
            var keyboardCaptureMock = new Mock<IKeyboardCaptureService>();
            keyboardCaptureMock.Setup(k => k.KeyboardKeyCapturePrompt).Returns("Prompt");
            var settingsServiceMock = new Mock<ISettingsService>();
            settingsServiceMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

            using var main = new MainViewModel(
                profileService: profileServiceMock.Object,
                keyboardCaptureService: keyboardCaptureMock.Object,
                gamepadReader: new Mock<IGamepadReader>().Object,
                processTargetService: new Mock<IProcessTargetService>().Object,
                elevationHandler: new Mock<IElevationHandler>().Object,
                appStatusMonitor: new Mock<IAppStatusMonitor>().Object,
                mappingEngine: new Mock<IMappingEngine>().Object,
                settingsService: settingsServiceMock.Object,
                itemSelectionDialogService: new Mock<IItemSelectionDialogService>().Object,
                profileDomainService: new ProfileDomainService());

            main.ProfileListTabIndex = (int)MainViewModel.MainProfileWorkspaceTab.Mappings;

            var editor = main.MappingEditorPanel;
            var grid = new DataGrid
            {
                ItemsSource = main.Mappings,
                DataContext = editor,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                SelectionMode = DataGridSelectionMode.Extended
            };
            DataGridWorkspaceSelectionBehavior.SetRuleListKind(grid, WorkspaceRuleListKind.Mappings);

            var rowA = new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" }
            };
            var rowB = new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" }
            };
            main.Mappings.Add(rowA);
            main.Mappings.Add(rowB);

            var window = new Window { Content = grid, Width = 400, Height = 300 };
            try
            {
                window.Show();

                main.SelectedMapping = rowA;
                Assert.Same(rowA, main.SelectedMapping);

                grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                grid.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                grid.UnselectAll();
                grid.SelectedItems.Add(rowB);

                Assert.Same(rowB, main.SelectedMapping);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunSta(Action action)
    {
        Exception? caught = null;
        var t = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (caught is not null)
            throw caught;
    }
}
