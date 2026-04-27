using System.Numerics;
using GamepadMapping.Tests.Support;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using Moq;


namespace GamepadMapping.Tests.Core.Input;

public class RadialMenuControllerTests
{
    private readonly Mock<IRadialMenuHud> _mockRadialMenuHud;
    private readonly Mock<IKeyboardActionExecutor> _mockActionExecutor;
    private readonly Mock<IKeyboardActionCatalog> _mockKeyboardActionCatalog;
    private readonly RadialMenuController _controller;
    private readonly List<string> _mappedOutputCalls;
    private readonly List<string> _mappingStatusCalls;
    private readonly List<DispatchedOutput> _enqueuedOutputs;
    private readonly List<IActiveAction> _registeredActions;
    private readonly List<string> _unregisteredActions;

    public RadialMenuControllerTests()
    {
        _mockRadialMenuHud = new Mock<IRadialMenuHud>();
        _mockActionExecutor = new Mock<IKeyboardActionExecutor>();
        _mockKeyboardActionCatalog = new Mock<IKeyboardActionCatalog>();
        _mappedOutputCalls = new List<string>();
        _mappingStatusCalls = new List<string>();
        _enqueuedOutputs = new List<DispatchedOutput>();
        _registeredActions = new List<IActiveAction>();
        _unregisteredActions = new List<string>();

        _controller = new RadialMenuController(
            radialMenuHud: _mockRadialMenuHud.Object,
            ui: ImmediateUiSynchronization.Instance,
            setMappedOutput: s => _mappedOutputCalls.Add(s),
            setMappingStatus: s => _mappingStatusCalls.Add(s),
            enqueueOutput: (btn, trig, output, label, token) => _enqueuedOutputs.Add(output),
            getConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey,
            registerActiveAction: action => _registeredActions.Add(action),
            unregisterActiveAction: id => _unregisteredActions.Add(id),
            requestTemplateSwitch: null,
            catalog: _mockKeyboardActionCatalog.Object
        );
    }

    [Fact]
    public void Constructor_InitializesStateCorrectly()
    {
        Assert.Equal(string.Empty, _controller.Id);
        Assert.Empty(_controller.ActiveChord);
        Assert.Null(_controller.ActiveRadial);
        Assert.Equal(-1, _controller.CurrentSelectedIndex);
    }

    [Fact]
    public async Task CurrentSelectedIndex_ConcurrentUpdates_EnsuresLatestValueTakesEffect()
    {
        // Arrange
        var radialMenu = new RadialMenuDefinition { Id = "testId", DisplayName = "Test Menu" };
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item1" });
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item2" });
        _controller.SetDefinitions(new List<RadialMenuDefinition> { radialMenu }, null);
        _controller.TryOpen(new MappingEntry { RadialMenu = new RadialMenuBinding { RadialMenuId = "testId" } }, "source", out _);

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var index = i % 2; // Alternate between 0 and 1
            tasks.Add(Task.Run(() => _controller.CurrentSelectedIndex = index));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        // We expect the last value set to be the final value, but due to scheduling, it could be either 0 or 1.
        // The main point is that no exception occurred due to race conditions and a valid state is maintained.
        Assert.True(_controller.CurrentSelectedIndex == 0 || _controller.CurrentSelectedIndex == 1);
    }

    [Fact]
    public async Task UpdateSelection_ConcurrentCalls_EnsuresConsistentState()
    {
        // Arrange
        var radialMenu = new RadialMenuDefinition { Id = "testId", DisplayName = "Test Menu" };
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item1" });
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item2" });
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item3" });
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item4" });
        _controller.SetDefinitions(new List<RadialMenuDefinition> { radialMenu }, null);
        _controller.SetActionExecutor(_mockActionExecutor.Object);
        _controller.TryOpen(new MappingEntry { RadialMenu = new RadialMenuBinding { RadialMenuId = "testId" } }, "source", out _);

        var random = new Random();
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var x = (float)(random.NextDouble() * 2 - 1); // -1 to 1
            var y = (float)(random.NextDouble() * 2 - 1); // -1 to 1
            var stick = new Vector2(x, y);
            tasks.Add(Task.Run(() => _controller.UpdateSelection(stick, 0.5f, RadialMenuConfirmMode.ReleaseGuideKey)));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        // Verify no exceptions and a consistent state.
        Assert.NotNull(_controller.ActiveRadial);
        Assert.True(_controller.CurrentSelectedIndex >= -1 && _controller.CurrentSelectedIndex < radialMenu.Items.Count);
    }

    [Fact]
    public async Task TryOpenAndTryClose_ConcurrentCalls_EnsuresConsistentState()
    {
        // Arrange
        var radialMenu = new RadialMenuDefinition { Id = "testId", DisplayName = "Test Menu" };
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item1" });
        _controller.SetDefinitions(new List<RadialMenuDefinition> { radialMenu }, null);
        _controller.SetActionExecutor(_mockActionExecutor.Object);

        var openMapping = new MappingEntry { RadialMenu = new RadialMenuBinding { RadialMenuId = "testId" } };
        string sourceToken = "source";

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() => _controller.TryOpen(openMapping, sourceToken, out _)));
            tasks.Add(Task.Run(() => _controller.TryClose(radialMenu.Id, sourceToken, false)));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        // Verify no exceptions. The final state (open or closed) is non-deterministic but should be consistent.
        // Check that _activeRadial and _activeChord are in a valid state.
        Assert.True(_controller.ActiveRadial == null && _controller.Id == string.Empty || _controller.ActiveRadial != null && _controller.Id == "testId");
    }

    [Fact]
    public void UpdateSelection_RepeatedSameSector_InvokesHudUpdateSelectionOnce()
    {
        var radialMenu = new RadialMenuDefinition { Id = "testId", DisplayName = "Test Menu" };
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item1" });
        radialMenu.Items.Add(new RadialMenuItem { ActionId = "item2" });
        _controller.SetDefinitions(new List<RadialMenuDefinition> { radialMenu }, null);
        _controller.TryOpen(new MappingEntry { RadialMenu = new RadialMenuBinding { RadialMenuId = "testId" } }, "source", out var err);
        Assert.Null(err);

        var stick = new Vector2(0f, 1f);
        for (var i = 0; i < 50; i++)
            _controller.UpdateSelection(stick, 0.01f, RadialMenuConfirmMode.ReleaseGuideKey);

        _mockRadialMenuHud.Verify(h => h.UpdateSelection(It.IsAny<int>()), Times.Once);
    }

    // Helper method for simulating UI thread dispatch in tests if needed
    private void RunOnUiThread(Action action)
    {
        action();
    }
}



