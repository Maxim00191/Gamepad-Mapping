using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Services.Input;
using GamepadMapping.Tests.Support;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Services.Input;

public sealed class MappingManagerRadialHudLifecycleTests
{
    private static MappingEngine CreateEngine(
        IRadialMenuHud hud,
        bool ownsRadialMenuHud) =>
        new(
            new Mock<IKeyboardEmulator>().Object,
            new Mock<IMouseEmulator>().Object,
            canDispatchOutputLive: () => true,
            ui: ImmediateUiSynchronization.Instance,
            setMappedOutput: _ => { },
            setMappingStatus: _ => { },
            setComboHud: null,
            radialMenuHud: hud,
            ownsRadialMenuHud: ownsRadialMenuHud);

    private static InputFrame Frame(long t, GamepadButtons buttons, Vector2 left, Vector2 right) =>
        new(
            buttons,
            left,
            right,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            IsConnected: true,
            TimestampMs: t);

    private static List<RadialMenuDefinition> TwoItemRadialRight(string id = "rm1") =>
    [
        new RadialMenuDefinition
        {
            Id = id,
            DisplayName = "Test Radial",
            Joystick = "RightStick",
            Items =
            [
                new RadialMenuItem { ActionId = "a1" },
                new RadialMenuItem { ActionId = "a2" }
            ]
        }
    ];

    private static List<KeyboardActionDefinition> ActionsA1A2() =>
    [
        new KeyboardActionDefinition { Id = "a1", KeyboardKey = "E", Description = "First" },
        new KeyboardActionDefinition { Id = "a2", KeyboardKey = "F", Description = "Second" }
    ];

    private static List<MappingEntry> RadialOnLeftThumb(string radialMenuId = "rm1") =>
    [
        new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftThumb" },
            Trigger = TriggerMoment.Pressed,
            RadialMenu = new RadialMenuBinding { RadialMenuId = radialMenuId }
        }
    ];

    [Fact]
    public void ReplaceEngine_SharedRadialHud_DoesNotDisposeHud()
    {
        var hud = new Mock<IRadialMenuHud>();
        var profile = new Mock<IProfileService>();
        var a = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        var manager = new MappingManager(a, profile.Object);

        var b = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        manager.ReplaceEngine(b, comboLeadButtons: null);

        hud.Verify(h => h.Dispose(), Times.Never);
    }

    [Fact]
    public void ReplaceEngine_OwnedRadialHud_DisposesHudOnce()
    {
        var hud = new Mock<IRadialMenuHud>();
        var profile = new Mock<IProfileService>();
        var a = CreateEngine(hud.Object, ownsRadialMenuHud: true);
        var manager = new MappingManager(a, profile.Object);

        var b = CreateEngine(new Mock<IRadialMenuHud>().Object, ownsRadialMenuHud: true);
        manager.ReplaceEngine(b, comboLeadButtons: null);

        hud.Verify(h => h.Dispose(), Times.Once);
    }

    [Fact]
    public void ReplaceEngine_RadialWasOpen_SharedHud_HidesBeforeSwap()
    {
        var hud = new Mock<IRadialMenuHud>();
        var profile = new Mock<IProfileService>();
        var engineA = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        engineA.SetRadialMenuDefinitions(TwoItemRadialRight(), ActionsA1A2());
        var mappings = RadialOnLeftThumb();
        engineA.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engineA.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        hud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Once);

        var manager = new MappingManager(engineA, profile.Object);
        var engineB = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        engineB.SetRadialMenuDefinitions(TwoItemRadialRight(), ActionsA1A2());

        manager.ReplaceEngine(engineB, comboLeadButtons: null);

        hud.Verify(h => h.HideMenu(), Times.AtLeastOnce);
        hud.Verify(h => h.Dispose(), Times.Never);

        engineB.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engineB.ProcessInputFrame(Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        hud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.AtLeast(2));
    }

    [Fact]
    public void ReplaceEngine_AfterExecutableActionCached_SharedRadialHud_RadialStillOpens()
    {
        var hud = new Mock<IRadialMenuHud>();
        var profile = new Mock<IProfileService>();
        var engineA = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        var manager = new MappingManager(engineA, profile.Object);

        manager.LoadTemplate(new GameProfileTemplate
        {
            ProfileId = "p",
            DisplayName = "Test",
            Mappings = RadialOnLeftThumb(),
            RadialMenus = TwoItemRadialRight(),
            KeyboardActions = ActionsA1A2()
        });

        var mapping = Assert.Single(manager.Mappings);
        manager.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), allowOutput: true);
        manager.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), allowOutput: true);
        hud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Once);
        Assert.NotNull(mapping.ExecutableAction);

        var engineB = CreateEngine(hud.Object, ownsRadialMenuHud: false);
        manager.ReplaceEngine(engineB, comboLeadButtons: null);

        Assert.Null(mapping.ExecutableAction);

        manager.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), allowOutput: true);
        manager.ProcessInputFrame(Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), allowOutput: true);
        hud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.AtLeast(2));
    }
}
