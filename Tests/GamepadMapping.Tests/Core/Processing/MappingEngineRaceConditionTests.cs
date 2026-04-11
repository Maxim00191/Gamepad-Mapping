using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
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

/// <summary>
/// Tests for race conditions and state machine boundary conditions in the mapping engine.
/// Focuses on edge cases where Hold threshold is reached, buttons are released, or new buttons are pressed
/// at critical timing boundaries.
/// </summary>
public class MappingEngineRaceConditionTests
{
    private static MappingEngine CreateEngine(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        Func<bool>? canDispatchOutput = null,
        ITimeProvider? timeProvider = null) =>
        new(
            keyboard,
            mouse,
            canDispatchOutput ?? (() => true),
            runOnUi: action => action(),
            setMappedOutput: _ => { },
            setMappingStatus: _ => { },
            setComboHud: null,
            requestTemplateSwitchToProfileId: null,
            timeProvider: timeProvider);

    private class MockTimeProvider : ITimeProvider
    {
        public long Ticks { get; set; }
        public List<MockTimer> Timers { get; } = new();

        public long GetTickCount64() => Ticks;

        public long GetPerformanceTimestamp() => (Ticks * Stopwatch.Frequency) / 1000;

        public ITimer CreateTimer(TimeSpan interval, Action onTick)
        {
            var timer = new MockTimer(interval, onTick, Ticks);
            Timers.Add(timer);
            return timer;
        }

        public void Advance(long ms)
        {
            Ticks += ms;
            foreach (var timer in Timers.Where(t => t.IsRunning).ToList())
            {
                if (Ticks >= timer.FireAtTicks)
                {
                    timer.Tick();
                    timer.FireAtTicks = Ticks + (long)timer.Interval.TotalMilliseconds;
                }
            }
        }
    }

    private class MockTimer : ITimer
    {
        public TimeSpan Interval { get; set; }
        private readonly Action _onTick;
        public bool IsRunning { get; private set; }
        public long FireAtTicks { get; set; }

        public MockTimer(TimeSpan interval, Action onTick, long startTicks)
        {
            Interval = interval;
            _onTick = onTick;
            FireAtTicks = startTicks + (long)interval.TotalMilliseconds;
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

    private static List<MappingEntry> HoldDualMappingB() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" },
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

    /// <summary>
    /// Test: Hold threshold reached exactly when button is released.
    /// Expected: Hold fires, then immediately releases. No Tap should be triggered.
    /// Race condition: Ticks == HoldThreshold AND button released in same frame.
    /// </summary>
    [Fact]
    public async Task HoldThresholdReachedExactlyAtRelease_HoldFires_NoTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        // T=0: Initialize state
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: Advance exactly to hold threshold (200ms from press)
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();
        
        // Hold should fire
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Release A
        engine.ProcessInputFrame(Frame(201, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        // Tap should NOT fire because Hold already fired
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Test: Hold threshold reached, then new button pressed 1ms later.
    /// Expected: Hold fires for A, then immediately B is pressed and its hold starts.
    /// State machine should handle independent hold sessions.
    /// </summary>
    [Fact]
    public async Task HoldThresholdReachedThenNewButtonPressed_BothCanHold()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "T",
                HoldKeyboardKey = "H",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" },
                KeyboardKey = "U",
                HoldKeyboardKey = "I",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            }
        };

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: A's hold fires
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // T=202ms: B pressed while A's hold is active
        mockTime.Advance(1);
        engine.ProcessInputFrame(Frame(202, GamepadButtons.A | GamepadButtons.B), mappings);
        await engine.WaitForIdleAsync();

        // A's hold already fired, B's hold should start independently
        // After advancing another 200ms from B's press
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        // B's hold should fire
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.I, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Test: Hold fires 1ms past threshold, then gamepad disconnects (all buttons released).
    /// Expected: Hold fires normally, release cleans up properly.
    /// </summary>
    [Fact]
    public async Task HoldFiredThenGamepadDisconnects_CleanupCorrectly()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: Hold fires
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // T=202ms: Gamepad disconnects (all buttons released)
        mockTime.Advance(1);
        engine.ProcessInputFrame(Frame(202, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        // No additional key presses should occur
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // Engine should not have crashed
    }

    /// <summary>
    /// Test: Hold is pending, then button released exactly 1ms before threshold.
    /// Expected: Hold should NOT fire, Tap should fire instead.
    /// </summary>
    [Fact]
    public async Task HoldReleasedOneMillisecondsBeforeThreshold_TapFires_NotHold()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = HoldDualMappingA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        // T=199ms: Release A, 1ms before 200ms threshold
        engine.ProcessInputFrame(Frame(199, GamepadButtons.None), mappings);

        await engine.WaitForIdleAsync();

        // Tap should fire because we released before threshold
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        // Hold should NOT fire
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Test: Hold fires, then in the next frame both the original button and another button are still pressed,
    /// then another button is pressed 1ms later forming a chord.
    /// Expected: Chord binding should NOT supersede already-fired hold.
    /// </summary>
    [Fact]
    public async Task HoldFiredThenChordFormedLater_HoldNotSuppressed()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = ChordWithHoldA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: A's hold fires
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // T=202ms: B pressed while A is held (forming A+B chord)
        mockTime.Advance(1);
        engine.ProcessInputFrame(Frame(202, GamepadButtons.A | GamepadButtons.B), mappings);
        await engine.WaitForIdleAsync();

        // Chord should trigger, but this shouldn't affect the already-fired hold
        mockKeyboard.Verify(k => k.KeyDown(Key.C), Times.Once);

        // Release both
        engine.ProcessInputFrame(Frame(203, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyUp(Key.C), Times.Once);
        // Tap should not fire because Hold already fired
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Test: Hold at exactly threshold boundary with multiple frames of high-frequency input.
    /// Expected: State machine should converge to consistent state.
    /// Scenario: A presses and releases before threshold (Tap fires), then presses again and fires Hold.
    /// </summary>
    [Fact]
    public async Task HoldBoundaryWithHighFrequencyFrames_StateConvergesCorrectly()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=196ms: Before threshold, release (will trigger Tap)
        mockTime.Advance(195);
        engine.ProcessInputFrame(Frame(196, GamepadButtons.None), mappings);

        // T=197ms: Press A again
        mockTime.Advance(1);
        engine.ProcessInputFrame(Frame(197, GamepadButtons.A), mappings);

        // T=397ms: New hold session for second press reaches threshold
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        // Should have: Tap from first press (released before threshold) + Hold from second press
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // Release
        engine.ProcessInputFrame(Frame(398, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        // No additional taps (Hold already fired)
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Test: Multiple holds pending, then all buttons released simultaneously.
    /// Expected: All hold sessions should be cleaned up properly.
    /// </summary>
    [Fact]
    public async Task MultipleHoldsPendingThenReleaseAll_AllCleansUp()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Q",
                HoldKeyboardKey = "W",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            },
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" },
                KeyboardKey = "E",
                HoldKeyboardKey = "R",
                HoldThresholdMs = 200,
                Trigger = TriggerMoment.Tap
            }
        };

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press both A and B
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A | GamepadButtons.B), mappings);
        
        // T=100ms: Still held, before threshold
        engine.ProcessInputFrame(Frame(100, GamepadButtons.A | GamepadButtons.B), mappings);

        // T=100ms: Release both before threshold
        engine.ProcessInputFrame(Frame(100, GamepadButtons.None), mappings);

        await engine.WaitForIdleAsync();

        // Both should tap, not hold (released before threshold)
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Q, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        // No holds should fire
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.W, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.R, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Test: Hold fires but output dispatch is blocked (canDispatchOutput returns false).
    /// Expected: Hold action is suppressed but session is still cleaned up on release.
    /// </summary>
    [Fact]
    public async Task HoldFiresButDispatchBlocked_SessionCleanedUpOnRelease()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        var canDispatch = new Func<bool>(() => false);
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, canDispatchOutput: canDispatch, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: Hold would fire but dispatch blocked
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        // No key output
        mockKeyboard.VerifyNoOtherCalls();

        // Release
        engine.ProcessInputFrame(Frame(202, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();

        // Still no output, engine should not crash
        mockKeyboard.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Test: Hold threshold boundary with exactly 0ms elapsed (immediate advance).
    /// Expected: Timer should fire at threshold, not before or after.
    /// </summary>
    [Fact]
    public async Task HoldTimerAtExactThreshold_FiresAtBoundary()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        var mappings = HoldDualMappingA();

        // T=0: Initialize
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // T=1: Press A
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);

        // T=201ms: Exactly at threshold
        mockTime.Advance(200);
        await engine.WaitForIdleAsync();

        // Hold should fire exactly once
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        // T=201ms: Advance again by 0 (no new ticks)
        mockTime.Advance(0);
        await engine.WaitForIdleAsync();

        // Should still be only once
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}



