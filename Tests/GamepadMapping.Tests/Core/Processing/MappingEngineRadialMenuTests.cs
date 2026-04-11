using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using Moq;

using Xunit;
using ITimer = GamepadMapperGUI.Interfaces.Core.ITimer;

namespace GamepadMapping.Tests.Core.Processing;

public sealed class MappingEngineRadialMenuTests
{
    private sealed class MockTimeProvider : ITimeProvider
    {
        public long Ticks { get; set; }
        public List<MockTimer> Timers { get; } = new();

        public long GetTickCount64() => Ticks;

        public long GetPerformanceTimestamp() => (Ticks * Stopwatch.Frequency) / 1000;

        public ITimer CreateTimer(TimeSpan interval, Action onTick)
        {
            var timer = new MockTimer(interval, onTick);
            Timers.Add(timer);
            return timer;
        }

        public void Advance(long ms)
        {
            Ticks += ms;
            foreach (var timer in Timers.Where(t => t.IsRunning).ToList())
            {
                if (ms >= timer.Interval.TotalMilliseconds)
                    timer.Tick();
            }
        }
    }

    private sealed class MockTimer : ITimer
    {
        public TimeSpan Interval { get; set; }
        private readonly Action _onTick;
        public bool IsRunning { get; private set; }

        public MockTimer(TimeSpan interval, Action onTick)
        {
            Interval = interval;
            _onTick = onTick;
        }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Tick() => _onTick();
        public void Dispose() => Stop();
    }

    private static MappingEngine CreateEngine(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        IRadialMenuHud hud,
        Func<RadialMenuConfirmMode>? getRadialMenuConfirmMode = null,
        Func<float>? getRadialMenuStickEngagementThreshold = null,
        ITimeProvider? timeProvider = null) =>
        new(
            keyboard,
            mouse,
            canDispatchOutputLive: () => true,
            runOnUi: action => action(),
            setMappedOutput: _ => { },
            setMappingStatus: _ => { },
            setComboHud: null,
            radialMenuHud: hud,
            getRadialMenuStickEngagementThreshold: getRadialMenuStickEngagementThreshold,
            getRadialMenuConfirmMode: getRadialMenuConfirmMode,
            timeProvider: timeProvider);

    private static InputFrame Frame(
        long timestampMs,
        GamepadButtons buttons,
        Vector2 leftStick,
        Vector2 rightStick) =>
        new(
            buttons,
            leftStick,
            rightStick,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            IsConnected: true,
            TimestampMs: timestampMs);

    private static List<RadialMenuDefinition> TwoItemRadialRightStick(string id = "rm1") =>
        new()
        {
            new RadialMenuDefinition
            {
                Id = id,
                DisplayName = "Test Radial",
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>
                {
                    new RadialMenuItem { ActionId = "a1" },
                    new RadialMenuItem { ActionId = "a2" }
                }
            }
        };

    private static List<RadialMenuDefinition> TwoItemRadialLeftStick(string id = "rm1") =>
        new()
        {
            new RadialMenuDefinition
            {
                Id = id,
                DisplayName = "Test Radial L",
                Joystick = "LeftStick",
                Items = new ObservableCollection<RadialMenuItem>
                {
                    new RadialMenuItem { ActionId = "a1" },
                    new RadialMenuItem { ActionId = "a2" }
                }
            }
        };

    private static List<RadialMenuDefinition> EmptyItemsRadial(string id = "rmEmpty") =>
        new()
        {
            new RadialMenuDefinition
            {
                Id = id,
                DisplayName = "Empty",
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>()
            }
        };

    private static List<RadialMenuDefinition> RadialWithOrphanActionId(string id = "rm1") =>
        new()
        {
            new RadialMenuDefinition
            {
                Id = id,
                DisplayName = "Test Radial",
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>
                {
                    new RadialMenuItem { ActionId = "a1" },
                    new RadialMenuItem { ActionId = "not-in-catalog" }
                }
            }
        };

    private static List<KeyboardActionDefinition> ActionsA1A2() =>
        new()
        {
            new KeyboardActionDefinition { Id = "a1", KeyboardKey = "E", Description = "First" },
            new KeyboardActionDefinition { Id = "a2", KeyboardKey = "F", Description = "Second" }
        };

    private static List<MappingEntry> RadialOnLeftThumbPress(string radialMenuId = "rm1") =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftThumb" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = radialMenuId }
            }
        };

    private static List<MappingEntry> RadialOnAPlusBPress(string radialMenuId = "rm1") =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = radialMenuId }
            }
        };

    [Fact]
    public void OpenRadial_UsesSlotLabel_OverKeyboardActionDescription()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, mockHud.Object);

        var radial = new List<RadialMenuDefinition>
        {
            new()
            {
                Id = "rm1",
                DisplayName = "Test Radial",
                Joystick = "RightStick",
                Items = new ObservableCollection<RadialMenuItem>
                {
                    new RadialMenuItem { ActionId = "a1", Label = "HUD override" }
                }
            }
        };

        engine.SetRadialMenuDefinitions(radial, ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);

        mockHud.Verify(
            h => h.ShowMenu(
                "Test Radial",
                It.Is<IReadOnlyList<RadialMenuHudItem>>(items =>
                    items.Count == 1
                    && items[0].DisplayName == "HUD override"
                    && items[0].KeyboardKeyLabel == "E")),
            Times.Once);
    }

    /// <summary>
    /// Radial on a solo button defers on press when another mapping shares that button in a chord,
    /// even if the button is not listed in comboLeadButtons; short release opens the menu (same as combo-lead defer).
    /// </summary>
    [Fact]
    public async Task RadialSolo_DefersWhenChordSharesKey_OpensOnShortRelease_WithoutComboLeadList()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetComboLeadButtonsFromTemplate([]);
        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+X" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = "rm1" }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftShoulder, Vector2.Zero, Vector2.Zero), mappings);
        mockHud.Verify(
            h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()),
            Times.Never);

        engine.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        await engine.WaitForIdleAsync();

        mockHud.Verify(
            h => h.ShowMenu("Test Radial", It.Is<IReadOnlyList<RadialMenuHudItem>>(items => items.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task ReleaseGuide_SelectsSector_DispatchesTapOnChordRelease()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        mockHud.Verify(
            h => h.ShowMenu("Test Radial", It.Is<IReadOnlyList<RadialMenuHudItem>>(items => items.Count == 2)),
            Times.Once);

        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        mockHud.Verify(h => h.UpdateSelection(0), Times.Once);

        engine.ProcessInputFrame(Frame(3, GamepadButtons.None, Vector2.Zero, new Vector2(0f, 1f)), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReleaseGuide_RecenteringStickClearsSelection_ChordReleaseDispatchesNothing()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        engine.ProcessInputFrame(
            Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero),
            mappings);
        mockHud.Verify(h => h.UpdateSelection(-1), Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockKeyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnStickToCenter_SelectsOnStickReturn_DispatchesTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReturnStickToCenter);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        engine.ProcessInputFrame(
            Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero),
            mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockKeyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnStickToCenter_ReleasingChordWhileStickEngaged_DispatchesNothing()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReturnStickToCenter);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        engine.ProcessInputFrame(
            Frame(3, GamepadButtons.None, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockKeyboard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnStickToCenter_AfterCenterConfirm_ReopenRadialOnlyAfterPhysicalChordRelease()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReturnStickToCenter);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        engine.ProcessInputFrame(
            Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero),
            mappings);
        mockHud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(5, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Exactly(2));
        mockKeyboard.Verify(
            k => k.TapKeyAsync(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnknownRadialMenuId_DoesNotShowHud()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, mockHud.Object);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick("rm1"), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress("does-not-exist");

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Never);
        mockHud.Verify(h => h.HideMenu(), Times.Never);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void StickBelowEngagementThreshold_DoesNotSelectSector()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey,
            getRadialMenuStickEngagementThreshold: () => 0.5f);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 0.4f)),
            mappings);

        mockHud.Verify(h => h.UpdateSelection(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WhileRadialOpen_RightStickAnalogSuppressed_LeftStickAnalogStillProcesses()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, mockHud.Object);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftThumb" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = "rm1" }
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" },
                KeyboardKey = "W",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "Up" },
                KeyboardKey = "I",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);

        engine.ProcessInputFrame(
            Frame(
                2,
                GamepadButtons.LeftThumb,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f)),
            mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.W), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.I), Times.Never);
    }

    [Fact]
    public async Task WhileRadialOpen_LeftStickRadial_SuppressesLeftAnalog_RightAnalogStillProcesses()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, mockHud.Object);

        engine.SetRadialMenuDefinitions(TwoItemRadialLeftStick(), ActionsA1A2());
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftThumb" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = "rm1" }
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" },
                KeyboardKey = "W",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "Up" },
                KeyboardKey = "I",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, new Vector2(0f, 1f), new Vector2(0f, 1f)),
            mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.W), Times.Never);
        mockKeyboard.Verify(k => k.KeyDown(Key.I), Times.Once);
    }

    [Fact]
    public async Task ForceReleaseAllOutputs_RadialOpen_HidesHud_DoesNotDispatchSelection()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, mockHud.Object);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);

        engine.ForceReleaseAllOutputs();
        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DisconnectWhileRadialOpen_WithStickSelection_DoesNotDispatchTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(0f, 1f)),
            mappings);
        engine.ProcessInputFrame(InputFrame.Disconnected(3), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseGuide_ChordRadial_ReleasingOneChordButton_ClosesWithoutTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnAPlusBPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A | GamepadButtons.B, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.B, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EmptyRadialItems_OpensAndClosesWithoutTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(EmptyItemsRadial("rmEmpty"), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress("rmEmpty");

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.ShowMenu("Empty", It.Is<IReadOnlyList<RadialMenuHudItem>>(l => l.Count == 0)), Times.Once);
        mockHud.Verify(h => h.HideMenu(), Times.Once);
        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MissingKeyboardActionForItem_DoesNotEnqueueTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(RadialWithOrphanActionId(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(
            Frame(2, GamepadButtons.LeftThumb, Vector2.Zero, new Vector2(1f, 0f)),
            mappings);
        engine.ProcessInputFrame(Frame(3, GamepadButtons.None, Vector2.Zero, new Vector2(1f, 0f)), mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(
            k => k.TapKeyAsync(It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RapidOpenCloseOpen_ProducesTwoShowMenuCalls()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey);

        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());
        var mappings = RadialOnLeftThumbPress();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(3, GamepadButtons.LeftThumb, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(4, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()), Times.Exactly(2));
        mockHud.Verify(h => h.HideMenu(), Times.Exactly(2));
    }

    /// <summary>
    /// Solo button with both a keyboard action and a radial menu: short release sends Tap (click), not Pressed hold.
    /// </summary>
    [Fact]
    public async Task SoloKeyboardPlusRadial_NoChordInProfile_ShortRelease_TapsKeyboard_NotRadial()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();
        var mockTime = new MockTimeProvider();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey,
            timeProvider: mockTime);

        engine.SetComboLeadButtonsFromTemplate(["RightShoulder"]);
        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder" },
                KeyboardKey = "LeftClick",
                Trigger = TriggerMoment.Pressed
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = "rm1" }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);

        await engine.WaitForIdleAsync();

        mockHud.Verify(
            h => h.ShowMenu(It.IsAny<string>(), It.IsAny<IReadOnlyList<RadialMenuHudItem>>()),
            Times.Never);
        mockMouse.Verify(m => m.LeftClickAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoloKeyboardPlusRadial_NoChordInProfile_HoldPastThreshold_OpensRadial()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockHud = new Mock<IRadialMenuHud>();
        var mockTime = new MockTimeProvider();

        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            mockHud.Object,
            getRadialMenuConfirmMode: () => RadialMenuConfirmMode.ReleaseGuideKey,
            timeProvider: mockTime);

        engine.SetComboLeadButtonsFromTemplate(["RightShoulder"]);
        engine.SetRadialMenuDefinitions(TwoItemRadialRightStick(), ActionsA1A2());

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder" },
                KeyboardKey = "LeftClick",
                Trigger = TriggerMoment.Pressed
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder" },
                Trigger = TriggerMoment.Pressed,
                RadialMenu = new RadialMenuBinding { RadialMenuId = "rm1" }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None, Vector2.Zero, Vector2.Zero), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder, Vector2.Zero, Vector2.Zero), mappings);
        mockTime.Advance(600);

        await engine.WaitForIdleAsync();

        mockHud.Verify(
            h => h.ShowMenu("Test Radial", It.Is<IReadOnlyList<RadialMenuHudItem>>(items => items.Count == 2)),
            Times.Once);
        mockMouse.Verify(m => m.LeftClickAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}



