#nullable enable

using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using GamepadMapping.Tests.Support;
using Moq;

namespace GamepadMapping.Tests.Services.Input;

public sealed class PlayStationTouchpadHidToMappingTests
{
    [Fact]
    public void DualSenseReportsDecodedByProvider_DriveTouchpadMouseLook_OnSecondPoll()
    {
        var reportA = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 8,
                    xNorm: 0.5f,
                    yNorm: 0.5f));

        var reportB = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 8,
                    xNorm: 0.66f,
                    yNorm: 0.5f));

        var streamA = new FakeDualSenseHidStream([reportA]);
        var streamB = new FakeDualSenseHidStream([reportB]);
        var providerA = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(streamA));
        var providerB = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(streamB));

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
                KeyboardKey = "mousex",
                Trigger = TriggerMoment.Pressed
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        Assert.True(providerA.TryGetState(out var s1));
        sut.ProcessTouchpad(s1, mappings);

        Assert.True(providerB.TryGetState(out var s2));
        sut.ProcessTouchpad(s2, mappings);

        mouse.Verify(
            m => m.MoveBy(It.Is<int>(dx => dx != 0), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.AtLeastOnce);
    }

    [Fact]
    public void DualSenseReportsDecodedByProvider_SwipeLiftDispatches_WhenEncoderMatchesGesture()
    {
        var lift = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
            {
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: false,
                    trackingId: 0,
                    xNorm: 0f,
                    yNorm: 0f);
            });

        var strokeStart = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 1,
                    xNorm: 0.5f,
                    yNorm: 0.78f));

        var strokeEnd = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 1,
                    xNorm: 0.5f,
                    yNorm: 0.12f));

        var pStart = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(new FakeDualSenseHidStream([strokeStart])));
        var pMove = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(new FakeDualSenseHidStream([strokeEnd])));
        var pLift = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(new FakeDualSenseHidStream([lift])));

        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "SWIPE_UP" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Tap
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var dispatched = new List<MappingEntry>();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { },
            dispatchTouchpadDiscreteAction: dispatched.Add);

        Assert.True(pStart.TryGetState(out var a));
        sut.ProcessTouchpad(a, mappings);
        Assert.True(pMove.TryGetState(out var b));
        sut.ProcessTouchpad(b, mappings);
        Assert.True(pLift.TryGetState(out var c));
        sut.ProcessTouchpad(c, mappings);

        Assert.Single(dispatched);
        Assert.Equal("SWIPE_UP", dispatched[0].From!.Value);
    }
}
