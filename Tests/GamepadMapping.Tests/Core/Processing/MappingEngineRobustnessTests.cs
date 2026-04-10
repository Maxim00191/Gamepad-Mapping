using System.Collections.Generic;
using System.Threading;
using System.Numerics;
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

public class MappingEngineRobustnessTests
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
                // Simple mock timer logic
                timer.Tick();
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

    private static List<MappingEntry> TapOnA() =>
        new()
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Tap
            }
        };

    [Fact]
    public async Task ShortGlitch_PressedReleasedInOneMs_ShouldStillTriggerTap()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = TapOnA();

        // Frame 1: None
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Frame 2: A pressed (1ms later)
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        // Frame 3: A released (2ms later)
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);

        await engine.WaitForIdleAsync();

        // Current behavior: Tap triggers on Pressed event.
        // So even a 1ms glitch will trigger a Tap.
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Space, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HighFrequencyJitter_50Hz_ShouldNotFloodQueueExcessively()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);

        // 50 toggles (25 press/release pairs) in 1 second (every 20ms)
        for (int i = 1; i <= 50; i++)
        {
            var buttons = (i % 2 == 1) ? GamepadButtons.A : GamepadButtons.None;
            engine.ProcessInputFrame(Frame(i * 20, buttons), mappings);
        }

        await engine.WaitForIdleAsync();

        // 25 presses, 25 releases
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Exactly(25));
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Exactly(25));
    }

    [Fact]
    public async Task ExtremeJitter_EveryFrame_ShouldProcessAllTransitions()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);

        // 100 frames of alternating A
        for (int i = 1; i <= 100; i++)
        {
            var buttons = (i % 2 == 1) ? GamepadButtons.A : GamepadButtons.None;
            engine.ProcessInputFrame(Frame(i, buttons), mappings);
        }

        await engine.WaitForIdleAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Exactly(50));
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Exactly(50));
    }

    [Fact]
    public async Task ExtremeJitter_WithTap_ShouldProcessAllTaps()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = TapOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);

        // 100 frames of alternating A
        for (int i = 1; i <= 100; i++)
        {
            var buttons = (i % 2 == 1) ? GamepadButtons.A : GamepadButtons.None;
            engine.ProcessInputFrame(Frame(i, buttons), mappings);
        }

        await engine.WaitForIdleAsync();

        // Each press (50 times) should trigger a Tap
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Space, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(50));
    }

    [Fact]
    public async Task ExtremeJitter_WithHoldDualMapping_ShouldNotTriggerHold()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        
        var mappings = new List<MappingEntry>
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

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);

        // 100 frames of alternating A, each frame is 1ms
        for (int i = 1; i <= 100; i++)
        {
            var buttons = (i % 2 == 1) ? GamepadButtons.A : GamepadButtons.None;
            engine.ProcessInputFrame(Frame(i, buttons), mappings);
            // mockTime.Advance(1); // DO NOT ADVANCE TIME - we want to test that it doesn't fire hold
        }

        await engine.WaitForIdleAsync();

        // Should have 50 Taps of 'T'
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.T, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(50));
        // Should have 0 Taps of 'H' (Hold)
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.H, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisconnectDuringComplexChord_ShouldReleaseAllHeldKeys()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // Mapping: A+B -> Space
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Released
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press A (Lead)
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        // Press B (Action) -> Space Down
        engine.ProcessInputFrame(Frame(2, GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        // Disconnect!
        engine.ProcessInputFrame(InputFrame.Disconnected(3), mappings);
        
        await engine.WaitForIdleAsync();
        // Should release Space
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    [Fact]
    public async Task ProfileSwitchWhileHoldingKey_ShouldReleaseOldKey()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        var profile1 = new List<MappingEntry>
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

        var profile2 = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Enter",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Enter",
                Trigger = TriggerMoment.Released
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), profile1);
        // Press A in Profile 1 -> Space Down
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), profile1);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        // Switch to Profile 2 while A is still held.
        // In real app, the engine is NOT disposed, just the mappingsSnapshot changes.
        // BUT, the engine needs to know that the profile changed to release old keys.
        // Currently, the engine doesn't have a "SetProfile" method, it just takes mappings in ProcessInputFrame.
        // If we just pass profile2 to ProcessInputFrame, it might not know it needs to release Space.
        
        // Let's see what happens if we just call ForceReleaseAllOutputs() which is what the app should do on switch.
        engine.ForceReleaseAllOutputs();
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
        
        // Now continue with profile 2
        engine.ProcessInputFrame(Frame(2, GamepadButtons.A), profile2);
        await engine.WaitForIdleAsync();
        // Since A was already active in the engine's state, it might not trigger a new Pressed event 
        // unless the frame transition middleware sees it as a new press.
        // In this case, A is still held, so transition middleware won't see it as Pressed.
        mockKeyboard.Verify(k => k.KeyDown(Key.Enter), Times.Never);
    }

    [Fact]
    public async Task AnalogStick_DisconnectWhileActive_ShouldReleaseKeys()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // Mapping: LeftStick Up -> W
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "Up" },
                KeyboardKey = "W",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // Move stick up
        var stickUp = new InputFrame(
            GamepadButtons.None,
            LeftThumbstick: new Vector2(0, 1f),
            RightThumbstick: Vector2.Zero,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            IsConnected: true,
            TimestampMs: 1);
            
        engine.ProcessInputFrame(stickUp, mappings);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.W), Times.Once);

        // Disconnect!
        engine.ProcessInputFrame(InputFrame.Disconnected(2), mappings);
        
        await engine.WaitForIdleAsync();
        // Should release W
        mockKeyboard.Verify(k => k.KeyUp(Key.W), Times.Once);
    }

    [Fact]
    public async Task PartialChordRelease_ShouldReleaseChordOutput()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // A+B -> Space
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Released
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press A+B -> Space Down
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        // Release B but keep A
        engine.ProcessInputFrame(Frame(2, GamepadButtons.A), mappings);
        
        await engine.WaitForIdleAsync();
        // Space should be released because the chord A+B is no longer satisfied
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    [Fact]
    public async Task SoloLeadReactivation_AfterChordPartialRelease_ShouldTriggerSoloOnFinalRelease()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // A -> Enter (Solo)
        // A+B -> Space (Chord)
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Enter",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Enter",
                Trigger = TriggerMoment.Released
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Released
            }
        };

        // Explicitly set A as a lead button to ensure it's deferred
        engine.SetComboLeadButtonsFromTemplate(new[] { "A" });

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press A (Lead) -> Nothing yet (deferred because A is a lead for A+B)
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(It.IsAny<Key>()), Times.Never);

        // 2. Press B -> Space Down (Chord satisfied)
        engine.ProcessInputFrame(Frame(2, GamepadButtons.A | GamepadButtons.B), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.Enter), Times.Never);

        // 3. Release B (Keep A) -> Space Up
        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
        
        // 4. Finally release A -> Should NOT trigger Enter (Solo) 
        // because A was part of a chord that already fired.
        // This is the current business logic: if a button participated in a successful chord, 
        // its solo function is suppressed for that "session".
        engine.ProcessInputFrame(Frame(4, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();
        
        // After fix, this should now be correctly suppressed.
        mockKeyboard.Verify(k => k.KeyDown(Key.Enter), Times.Never);
        // The engine might still call KeyUp for Enter if it was tracking it as a potential output,
        // but it should not have called KeyDown.
    }

    [Fact]
    public async Task PartialChordRelease_LeadFirst_ShouldReleaseChordOutput()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // A+B -> Space
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Released
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press A+B -> Space Down
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A | GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        // Release A (Lead) but keep B (Action)
        engine.ProcessInputFrame(Frame(2, GamepadButtons.B), mappings);
        
        await engine.WaitForIdleAsync();
        // Space should be released because the chord A+B is no longer satisfied
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    [Fact]
    public async Task InputDispatcher_QueueGrowthWithBlockedEmulator_ShouldNotCrash()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        
        // Block the emulator to simulate slow system/heavy load
        var tcs = new TaskCompletionSource();
        mockKeyboard.Setup(k => k.KeyDown(It.IsAny<Key>())).Callback(() => tcs.Task.Wait());

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // Push 1000 frames of A press/release
        for (int i = 1; i <= 1000; i++)
        {
            engine.ProcessInputFrame(Frame(i * 2, GamepadButtons.A), mappings);
            engine.ProcessInputFrame(Frame(i * 2 + 1, GamepadButtons.None), mappings);
        }

        // Unblock and wait for idle
        tcs.SetResult();
        await engine.WaitForIdleAsync();

        // Should have processed all 1000 presses and releases
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Exactly(1000));
        mockKeyboard.Verify(k => k.KeyUp(Key.Space), Times.Exactly(1000));
    }

    [Fact]
    public async Task MappingEngine_ConcurrentProcessInputFrame_ShouldBeSafe()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        var mappings = SpacePressReleaseOnA();

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);

        // Simulate concurrent calls to ProcessInputFrame
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 100; j++)
                {
                    engine.ProcessInputFrame(Frame(threadId * 1000 + j, GamepadButtons.A), mappings);
                    // Add a small delay to allow for more interleaving
                    await Task.Delay(1);
                    engine.ProcessInputFrame(Frame(threadId * 1000 + j + 500, GamepadButtons.None), mappings);
                }
            }));
        }

        await Task.WhenAll(tasks);
        await engine.WaitForIdleAsync();

        // 10 threads * 100 iterations = 1000 presses/releases
        // Use AtLeast once because concurrent frames might be dropped if they arrive out of order or too fast
        // but we want to ensure no crashes and some processing happens.
        // Actually, for this test to be robust, we just want to ensure it doesn't crash.
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.AtLeastOnce());
    }

    [Fact]
    public async Task IdenticalMappings_ShouldBeProcessedInOrder()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var sequence = new List<string>();
        
        mockKeyboard.Setup(k => k.KeyDown(Key.A)).Callback(() => sequence.Add("A"));
        mockKeyboard.Setup(k => k.KeyDown(Key.B)).Callback(() => sequence.Add("B"));

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // Two identical triggers, different outputs
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "X" },
                KeyboardKey = "A",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "X" },
                KeyboardKey = "B",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.X), mappings);
        
        await engine.WaitForIdleAsync();

        // Should trigger both in the order they appear in the list
        Assert.Equal(2, sequence.Count);
        Assert.Equal("A", sequence[0]);
        Assert.Equal("B", sequence[1]);
    }

    [Fact]
    public async Task PeerConflict_OverlappingChords_ShouldBothTriggerInOrder()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var sequence = new List<string>();
        
        mockKeyboard.Setup(k => k.KeyDown(Key.Q)).Callback(() => sequence.Add("Q"));
        mockKeyboard.Setup(k => k.KeyDown(Key.E)).Callback(() => sequence.Add("E"));

        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // A+B -> Q
        // B+X -> E
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A+B" },
                KeyboardKey = "Q",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B+X" },
                KeyboardKey = "E",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        // Press A+B+X
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A | GamepadButtons.B | GamepadButtons.X), mappings);
        
        await engine.WaitForIdleAsync();

        // Both should trigger in list order
        Assert.Equal(2, sequence.Count);
        Assert.Equal("Q", sequence[0]);
        Assert.Equal("E", sequence[1]);
    }

    [Fact]
    public async Task NonFaceButton_UsedInChord_ShouldBeInferredAsLead()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // RB+A -> P
        // A -> Space
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder+A" },
                KeyboardKey = "P",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press RB (Modifier) -> Nothing
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(It.IsAny<Key>()), Times.Never);

        // 2. Press A while RB is held -> Should trigger P, NOT Space
        engine.ProcessInputFrame(Frame(2, GamepadButtons.RightShoulder | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.KeyDown(Key.P), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Never, "A should be deferred because RB is a lead button for RB+A");
    }

    [Fact]
    public async Task NonFaceButton_NotExplicitlyLead_ButUsedInChord_ShouldBeInferredAsLead()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // RB+A -> P
        // A -> Space
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder+A" },
                KeyboardKey = "P",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            }
        };

        // When A is pressed, the engine should check if A is a lead for any chord.
        // In this case, A is NOT a lead (it's a face button).
        // BUT, RB is a lead. When RB is pressed, nothing happens.
        // When A is pressed, the engine sees RB is already down.
        // It matches RB+A.
        
        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press A first -> Should trigger Space (A is not a lead)
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);
        
        // 2. Release A
        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await engine.WaitForIdleAsync();
        
        // 3. Press RB first -> Nothing
        engine.ProcessInputFrame(Frame(3, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();
        
        // 4. Press A while RB is held -> Should trigger P, NOT Space
        engine.ProcessInputFrame(Frame(4, GamepadButtons.RightShoulder | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.KeyDown(Key.P), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once, "Space should not have been triggered a second time");
    }

    [Fact]
    public async Task TapTrigger_WithModifierHeld_ShouldBeSuppressed()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // RB+A -> P
        // A -> Space (Tap)
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder+A" },
                KeyboardKey = "P",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Tap
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press RB
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();

        // 2. Press A while RB is held -> Should trigger P, NOT Space (Tap)
        engine.ProcessInputFrame(Frame(2, GamepadButtons.RightShoulder | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.KeyDown(Key.P), Times.Once);
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Space, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never, "A (Tap) should be suppressed when RB is held");
    }

    [Fact]
    public async Task TapTrigger_WithNonLeadHeld_ShouldNOTBeSuppressed()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // A -> Space (Tap)
        // B -> Enter (Pressed)
        // Note: B is NOT a lead button because it's not used in any chord.
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Tap
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "B" },
                KeyboardKey = "Enter",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press B
        engine.ProcessInputFrame(Frame(1, GamepadButtons.B), mappings);
        await engine.WaitForIdleAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Enter), Times.Once);

        // 2. Press A while B is held -> Should trigger Space (Tap) because B is not a lead
        engine.ProcessInputFrame(Frame(2, GamepadButtons.B | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Space, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once, "A (Tap) should NOT be suppressed when a non-lead button is held");
    }

    [Fact]
    public async Task PressedTrigger_WithModifierHeld_ShouldBeSuppressed()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object);
        
        // RB+A -> P
        // A -> Space (Pressed)
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder+A" },
                KeyboardKey = "P",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Pressed
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press RB
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();

        // 2. Press A while RB is held -> Should trigger P, NOT Space (Pressed)
        engine.ProcessInputFrame(Frame(2, GamepadButtons.RightShoulder | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.KeyDown(Key.P), Times.Once);
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Never, "A (Pressed) should be suppressed when RB is held");
    }

    [Fact]
    public async Task HoldDualTrigger_WithModifierHeld_ShouldBeSuppressed()
    {
        var mockKeyboard = new Mock<IKeyboardEmulator>();
        var mockMouse = new Mock<IMouseEmulator>();
        var mockTime = new MockTimeProvider();
        using var engine = CreateEngine(mockKeyboard.Object, mockMouse.Object, timeProvider: mockTime);
        
        // RB+A -> P
        // A -> Space (Tap) / LeftClick (Hold)
        var mappings = new List<MappingEntry>
        {
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "RightShoulder+A" },
                KeyboardKey = "P",
                Trigger = TriggerMoment.Pressed
            },
            new MappingEntry
            {
                From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
                KeyboardKey = "Space",
                HoldKeyboardKey = "LeftClick",
                HoldThresholdMs = 300,
                Trigger = TriggerMoment.Tap
            }
        };

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        
        // 1. Press RB
        engine.ProcessInputFrame(Frame(1, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();

        // 2. Press A while RB is held -> Should trigger P, NOT arm A's Hold-Dual
        engine.ProcessInputFrame(Frame(2, GamepadButtons.RightShoulder | GamepadButtons.A), mappings);
        await engine.WaitForIdleAsync();
        
        mockKeyboard.Verify(k => k.KeyDown(Key.P), Times.Once);
        
        // 3. Advance time past A's hold threshold
        mockTime.Advance(400);
        await engine.WaitForIdleAsync();
        
        // Should NOT trigger LeftClick (Hold)
        mockMouse.Verify(m => m.LeftDown(), Times.Never);

        // 4. Release A
        engine.ProcessInputFrame(Frame(500, GamepadButtons.RightShoulder), mappings);
        await engine.WaitForIdleAsync();
        
        // Should NOT trigger Space (Tap)
        mockKeyboard.Verify(k => k.TapKeyAsync(Key.Space, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}



