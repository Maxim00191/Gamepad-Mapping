using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Moq;
using Vortice.XInput;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing;

public class MappingEngineTests
{
    private static MappingEngine CreateEngine(
        IKeyboardEmulator keyboard,
        IMouseEmulator mouse,
        Func<bool>? canDispatchOutput = null,
        Action<string>? requestTemplateSwitchToProfileId = null) =>
        new(
            keyboard,
            mouse,
            canDispatchOutput ?? (() => true),
            runOnUi: action => action(),
            setMappedOutput: _ => { },
            setMappingStatus: _ => { },
            setComboHud: null,
            requestTemplateSwitchToProfileId: requestTemplateSwitchToProfileId);

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

    /// <summary>Mapped key output is applied on a background worker; allow the queue to drain before asserting.</summary>
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

        await FlushMappedOutputQueueAsync();

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
        await FlushMappedOutputQueueAsync();

        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        engine.ProcessInputFrame(InputFrame.Disconnected(2), mappings);
        await FlushMappedOutputQueueAsync();

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

        await FlushMappedOutputQueueAsync();

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
        await FlushMappedOutputQueueAsync();
        mockKeyboard.VerifyNoOtherCalls();

        engine.ProcessInputFrame(Frame(2, GamepadButtons.A | GamepadButtons.B), mappings);
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.R), Times.Once);

        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(k => k.KeyUp(Key.R), Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None), mappings);
        await FlushMappedOutputQueueAsync();

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
        await FlushMappedOutputQueueAsync();

        mockKeyboard.Verify(
            k => k.TapKey(Key.E, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
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

        engine.ProcessInputFrame(Frame(0, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(1, GamepadButtons.A), mappings);
        await FlushMappedOutputQueueAsync();

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
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);

        engine.ForceReleaseAllOutputs();
        await FlushMappedOutputQueueAsync();

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
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChord(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D1,
                It.IsAny<int>()),
            Times.Once);

        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(3, GamepadButtons.A), mappings);
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChord(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D2,
                It.IsAny<int>()),
            Times.Once);

        engine.ProcessInputFrame(Frame(4, GamepadButtons.None), mappings);
        engine.ProcessInputFrame(Frame(5, GamepadButtons.A), mappings);
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChord(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D3,
                It.IsAny<int>()),
            Times.Once);

        mockKeyboard.Verify(
            k => k.TapKeyChord(It.IsAny<IReadOnlyList<Key>>(), It.IsAny<Key>(), It.IsAny<int>()),
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
        await FlushMappedOutputQueueAsync();

        mockKeyboard.Verify(
            k => k.TapKeyChord(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 1 && l[0] == Key.LeftAlt),
                Key.D1,
                It.IsAny<int>()),
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
        await FlushMappedOutputQueueAsync();

        mockKeyboard.Verify(k => k.TapKey(Key.Q, 1, 0, It.IsAny<int>()), Times.Once);
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
        await FlushMappedOutputQueueAsync();
        mockKeyboard.VerifyNoOtherCalls();

        engine.ProcessInputFrame(Frame(2, GamepadButtons.None), mappings);
        await FlushMappedOutputQueueAsync();
        mockKeyboard.Verify(
            k => k.TapKeyChord(
                It.Is<IReadOnlyList<Key>>(l => l.Count == 0),
                Key.D1,
                It.IsAny<int>()),
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
        await FlushMappedOutputQueueAsync();

        Assert.Empty(switched);
        mockKeyboard.VerifyNoOtherCalls();
        mockMouse.VerifyNoOtherCalls();
    }
}
