using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Moq;

using Xunit;
using ITimer = GamepadMapperGUI.Interfaces.Core.ITimer;

namespace GamepadMapping.Tests.Core.Processing;

public class MappingEngineTests
{
    private static MappingEngine CreateEngine(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        Func<bool>? canDispatchOutput = null,
        Action<string>? requestTemplateSwitchToProfileId = null,
        ITimeProvider? timeProvider = null) =>
        new(
            keyboard,
            mouse,
            canDispatchOutput ?? (() => true),
            runOnUi: action => action(),
            setMappedOutput: _ => { },
            setMappingStatus: _ => { },
            setComboHud: null,
            requestTemplateSwitchToProfileId: requestTemplateSwitchToProfileId,
            timeProvider: timeProvider);

    private class MockTimeProvider : ITimeProvider
    {
        public long Ticks { get; set; }
        public List<MockTimer> Timers { get; } = new();

        public long GetTickCount64() => Ticks;

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
                {
                    timer.Tick();
                }
            }
        }
    }

    private class MockTimer : ITimer
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

    private static InputFrame Frame(long timestampMs, GamepadButtons buttons) =>
        new(
            buttons,
            LeftThumbstick: Vector2.Zero,
            RightThumbstick: Vector2.Zero,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            IsConnected: true,
            TimestampMs: timestampMs);

    private static List<MappingEntry> SpacePressReleaseOnA() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Released
            }
        };

    private static List<MappingEntry> LetterRPressReleaseOnAPlusB() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "R",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "R",
                Trigger = TriggerMoment.Released
            }
        };

    private static List<MappingEntry> LetterEOnATap() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "E",
                Trigger = TriggerMoment.Tap
            }
        };

    private static List<MappingEntry> HoldDualMappingA() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "T",
                HoldKeyboardKey = "H",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            }
        };

    private static List<MappingEntry> ChordWithHoldA() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "T",
                HoldKeyboardKey = "H",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "C",
                Trigger = TriggerMoment.Pressed
            }
        };

    /// <summary>Mapped key output is applied on a background worker; allow the queue to drain before asserting.</summary>
    [Obsolete("Use engine.WaitForIdleAsync() instead.")]
    private static Task FlushMappedOutputQueueAsync() => Task.Delay(200);

    [Fact]
    public async Task ProcessInputFrame_ButtonMappedToKey_KeyDownAndKeyUp()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once, "按下手柄 A 时应触发一次 Space 按下");
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once, "松开手柄 A 时应触发一次 Space 松开");
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    /// <summary>
    /// When the pad drops, <see cref="GamepadReader"/> emits <see cref="InputFrame.Disconnected"/> (buttons cleared).
    /// Those synthetic releases must run <c>Released</c> mappings so keys are not left down.
    /// </summary>
    [Fact]
    public async Task ProcessInputFrame_DisconnectWhileButtonHeld_ReleasesMappedKey()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        engine.ProcessInputFrame(InputFrame.Disconnected(2), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once, "断开连接时应释放仍按着的映射键，避免卡键");
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_UnmappedButton_NoKeyboardOrMouseCalls()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = new List<MappingEntry>();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.B), mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_ChordRequiresBothButtons_KeyDownOnlyWhenChordComplete()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = LetterRPressReleaseOnAPlusB();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.VerifyNoOtherCalls();

        engine.ProcessInputFrame(Frame(2, GamepadButtons.A | GamepadButtons.B), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.R), Times.Once);

        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyUp(Key.R), Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_TapTrigger_InvokesTapKeyOnceOnPress()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = LetterEOnATap();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(
            k => k.TapKeyAsync(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(It.IsAny<Key>()), Times.Never);
        mockKeyboard.Verify(k => k.KeyUp(It.IsAny<Key>()), Times.Never);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_CannotDispatchOutput_SuppressesPress_NotKeyboardDown()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, () => false);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings, canDispatchMappedOutput: false);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings, canDispatchMappedOutput: false);
        await engine.WaitForIdleAsync();

        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ForceReleaseAllOutputs_AfterKeyHeld_SendsKeyUp()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        engine.ForceReleaseAllOutputs();
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.AtLeastOnce);
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);
    }

    [Fact]
    public async Task ProcessInputFrame_ItemCycleNext_TapKeyChordCyclesDigits()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                Trigger = TriggerMoment.Tap,
                ItemCycle = new ItemCycleBinding { Direction = ItemCycleDirection.Next, SlotCount = 3 }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D1,
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D2,
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(5, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D3,
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(It.IsAny<IReadOnlyList<Key>>(), It.IsAny<Key>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_ItemCycleWithModifiers_PassesModifiersToTapKeyChord()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                Trigger = TriggerMoment.Tap,
                ItemCycle = new ItemCycleBinding
                {
                    Direction = ItemCycleDirection.Next,
                    SlotCount = 2,
                    WithKeys = new List<string> { "LeftAlt" }
                }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 1 && l[0] == Key.LeftAlt),
                Key.D1,
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_ItemCycleCustomLoopKeys_TapsConfiguredKeyForDirection()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                Trigger = TriggerMoment.Tap,
                ItemCycle = new ItemCycleBinding
                {
                    Direction = ItemCycleDirection.Next,
                    SlotCount = 3,
                    LoopForwardKey = "Q",
                    LoopBackwardKey = "E"
                }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Q, 1, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    /// <summary>
    /// D-pad keys are inferred combo leads when used in richer chords; solo presses defer to release.
    /// Item cycle uses no <see cref="MappingEntry.KeyboardKey"/>, so it must still dispatch on that short release.
    /// </summary>
    [Fact]
    public async Task ProcessInputFrame_DeferredComboLead_ItemCycleDispatchesOnShortRelease()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightThumb + DPadUp" },
                KeyboardKey = "F5",
                Trigger = TriggerMoment.Pressed
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "DPadUp" },
                Trigger = TriggerMoment.Pressed,
                ItemCycle = new ItemCycleBinding { Direction = ItemCycleDirection.Next, SlotCount = 3 }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.DPadUp), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.VerifyNoOtherCalls();

        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChordAsync(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D1,
                It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessInputFrame_TemplateTogglePressed_InvokesSwitchHandler()
    {
        var switched = new List<string>();
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            requestTemplateSwitchToProfileId: switched.Add);

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Y" },
                KeyboardKey = string.Empty,
                Trigger = TriggerMoment.Pressed,
                TemplateToggle = new TemplateToggleBinding { AlternateProfileId = "vehicle" }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.Y), mappings);

        Assert.Single(switched);
        Assert.Equal("vehicle", switched[0]);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_TemplateToggleReleased_DoesNotInvokeSwitchHandler()
    {
        var switched = new List<string>();
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(
            mockKeyboard.Object,
            mockMouse.Object,
            requestTemplateSwitchToProfileId: switched.Add);

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "Y" },
                KeyboardKey = string.Empty,
                Trigger = TriggerMoment.Released,
                TemplateToggle = new TemplateToggleBinding { AlternateProfileId = "vehicle" }
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.Y), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        Assert.Empty(switched);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessInputFrame_HoldDualMapping_ShortPress_TriggersTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = HoldDualMappingA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        // Release before 200ms threshold
        engine.ProcessInputFrame(Frame(100, GamepadButtons.None), mappings);

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInputFrame_HoldDualMapping_LongPress_TriggersHold()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // Advance time past threshold (200ms)
        mockTime.Advance(250);

        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // Release after hold fired
        engine.ProcessInputFrame(Frame(400, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        // Should NOT trigger Tap on release if Hold already fired
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInputFrame_HoldInterruptedByChord_CancelsHold_TriggersChord()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = ChordWithHoldA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        
        // Before hold threshold, press B to complete A+B chord
        engine.ProcessInputFrame(Frame(50, GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.C), Times.Once);

        // Advance time past original hold threshold
        mockTime.Advance(250);
        await engine.WaitForIdleAsync();

        // Hold should have been cancelled by the more specific chord
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        // Release both
        engine.ProcessInputFrame(Frame(400, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyUp(Key.C), Times.Once);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInputFrame_HighFrequencyJitter_HandlesCorrectly()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        // High frequency A press/release/press
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);

        await engine.WaitForIdleAsync();

        // Should have 2 KeyDowns and 1 KeyUp
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Exactly(2));
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    [Fact]
    public async Task ProcessInputFrame_OverlappingChords_TriggersAllMatchingChords()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // Mapping 1: LB+A -> Q
        // Mapping 2: LB+B -> E
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+A" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+B" },
                KeyboardKey = "E",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press LB+A+B
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftShoulder | GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();

        // Both should be triggered because both chords are satisfied
        mockKeyboard.Verify(k => k.KeyDown(Key.Q), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.E), Times.Once);
    }

    [Fact]
    public async Task ProcessInputFrame_OverlappingChordsWithDifferentSpecificity_TriggersBoth()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // Mapping 1: LB+A -> Q
        // Mapping 2: LB+A+B -> E
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+A" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "LeftShoulder+A+B" },
                KeyboardKey = "E",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press LB+A+B
        engine.ProcessInputFrame(Frame(1, GamepadButtons.LeftShoulder | GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();

        // Both should be triggered. Specificity is used to suppress *less specific* chords
        // ONLY IF they are marked as "Combo Leads" (deferred solo leads).
        // Since LB+A is already a chord (specificity 2), it's not a "solo lead" that gets deferred.
        mockKeyboard.Verify(k => k.KeyDown(Key.Q), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.E), Times.Once);
    }
}


