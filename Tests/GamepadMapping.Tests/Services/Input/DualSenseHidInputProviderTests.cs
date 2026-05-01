#nullable enable

using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using GamepadMapping.Tests.Support;

namespace GamepadMapping.Tests.Services.Input;

public sealed class DualSenseHidInputProviderTests
{
    [Fact]
    public void MaxDrainReadsPerPoll_CoversOneSecondOfExpectedDualSenseReports()
    {
        var minimumDrainWindowReports =
            DualSenseHidInputStreamConstraints.ExpectedMaxReportRateHz *
            DualSenseHidInputStreamConstraints.StaleReportDrainWindowMs /
            1_000;

        Assert.True(DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll >= minimumDrainWindowReports);
    }

    [Fact]
    public void TryGetState_WhenOneSecondOfStaleReportsQueued_ReturnsNewestReportInSinglePoll()
    {
        var reports = Enumerable
            .Repeat(DualSenseHidTestReportFactory.CreateReport(GamepadButtons.A), DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll)
            .Append(DualSenseHidTestReportFactory.CreateReport(GamepadButtons.B));
        var stream = new FakeDualSenseHidStream(reports);
        var provider = new DualSenseHidInputProvider(
            streamFactory: new FakeDualSenseHidStreamFactory(stream));

        var ok = provider.TryGetState(out var state);

        Assert.True(ok);
        Assert.Equal(GamepadButtons.B, state.Buttons);
        Assert.Equal(DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll + 1, stream.ReadCount);
        Assert.Equal(DualSenseHidInputStreamConstraints.PrimaryReadTimeoutMs, stream.ReadTimeout);
    }

    [Fact]
    public void TryGetState_WhenStaleQueueContainsMovingPrimaryTouch_ReturnsLatestNormalizedCoordinates()
    {
        var drainSpan = DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll;
        var stale = Enumerable.Range(0, drainSpan).Select(i =>
            DualSenseHidTestReportFactory.CreateReport(
                GamepadButtons.None,
                customizePayload: p =>
                    DualSenseHidReportTestEncoder.WriteTouchPoint(
                        p,
                        DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                        isActive: true,
                        trackingId: 5,
                        xNorm: i / (float)drainSpan,
                        yNorm: 0.5f)));

        const float newestX = 0.992f;
        var newest = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 5,
                    xNorm: newestX,
                        yNorm: 0.48f));

        var stream = new FakeDualSenseHidStream(stale.Append(newest));
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));
        DualSenseHidTestReportFactory.AssertNormalizedNear(newestX, state.PrimaryTouch.XNormalized);
        Assert.True(state.PrimaryTouch.IsActive);
        Assert.Equal(5, state.PrimaryTouch.TrackingId);
    }

    [Fact]
    public void TryGetState_WhenDrainTimesOutAfterFirstReport_ReturnsFirstReportWithoutDisconnecting()
    {
        var stream = new FakeDualSenseHidStream([DualSenseHidTestReportFactory.CreateReport(GamepadButtons.X)]);
        var provider = new DualSenseHidInputProvider(
            streamFactory: new FakeDualSenseHidStreamFactory(stream));

        var ok = provider.TryGetState(out var state);

        Assert.True(ok);
        Assert.Equal(GamepadButtons.X, state.Buttons);
        Assert.Equal(2, stream.ReadCount);
        Assert.Equal(DualSenseHidInputStreamConstraints.PrimaryReadTimeoutMs, stream.ReadTimeout);
    }

    [Fact]
    public void TryGetState_WhenOpenFails_ReturnsDisconnectedWithoutReading()
    {
        var stream = new FakeDualSenseHidStream([DualSenseHidTestReportFactory.CreateReport(GamepadButtons.A)]);
        var provider = new DualSenseHidInputProvider(
            streamFactory: new FakeDualSenseHidStreamFactory(stream, opens: false));

        var ok = provider.TryGetState(out var state);

        Assert.False(ok);
        Assert.Equal(default, state);
        Assert.Equal(0, stream.ReadCount);
    }

    [Fact]
    public void TryGetState_DecodesPrimaryTouch_AsNormalizedCoordinates()
    {
        const float xExpected = 0.25f;
        const float yExpected = 0.75f;
        var report = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
            {
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 4,
                    xNorm: xExpected,
                    yNorm: yExpected);
            });

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));

        Assert.True(state.PrimaryTouch.IsActive);
        Assert.Equal(4, state.PrimaryTouch.TrackingId);
        DualSenseHidTestReportFactory.AssertNormalizedNear(xExpected, state.PrimaryTouch.XNormalized);
        DualSenseHidTestReportFactory.AssertNormalizedNear(yExpected, state.PrimaryTouch.YNormalized);
    }

    [Fact]
    public void TryGetState_DecodesSecondaryTouch_IndependentlyFromPrimary()
    {
        var report = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
            {
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 1,
                    xNorm: 0.2f,
                    yNorm: 0.3f);
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.SecondaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 2,
                    xNorm: 0.82f,
                    yNorm: 0.71f);
            });

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));

        Assert.True(state.PrimaryTouch.IsActive);
        Assert.Equal(1, state.PrimaryTouch.TrackingId);
        DualSenseHidTestReportFactory.AssertNormalizedNear(0.2f, state.PrimaryTouch.XNormalized);

        Assert.True(state.SecondaryTouch.IsActive);
        Assert.Equal(2, state.SecondaryTouch.TrackingId);
        DualSenseHidTestReportFactory.AssertNormalizedNear(0.82f, state.SecondaryTouch.XNormalized);
        DualSenseHidTestReportFactory.AssertNormalizedNear(0.71f, state.SecondaryTouch.YNormalized);
    }

    [Fact]
    public void TryGetState_InactiveTouch_EncodesActiveBitPerDualSenseContract()
    {
        var report = DualSenseHidTestReportFactory.CreateReport(
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

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));
        Assert.False(state.PrimaryTouch.IsActive);
    }

    [Fact]
    public void TryGetState_TouchpadPhysicalClick_IsParsed()
    {
        var report = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p => DualSenseHidReportTestEncoder.WriteTouchpadClick(p, pressed: true));

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));
        Assert.True(state.IsTouchpadPressed);
    }

    [Fact]
    public void TryGetState_Report31PayloadSlice_DecodesTouchSameAsReport01()
    {
        const float xExpected = 0.6f;
        const float yExpected = 0.4f;

        var report = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            reportId: 0x31,
            customizePayload: p =>
            {
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 2,
                    xNorm: xExpected,
                    yNorm: yExpected);
            });

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));
        Assert.True(state.PrimaryTouch.IsActive);
        Assert.Equal(2, state.PrimaryTouch.TrackingId);
        DualSenseHidTestReportFactory.AssertNormalizedNear(xExpected, state.PrimaryTouch.XNormalized);
        DualSenseHidTestReportFactory.AssertNormalizedNear(yExpected, state.PrimaryTouch.YNormalized);
    }

    [Fact]
    public void TryGetState_RawTouchAtExtents_NormalizesToUnitRange()
    {
        var report = DualSenseHidTestReportFactory.CreateReport(
            GamepadButtons.None,
            customizePayload: p =>
            {
                DualSenseHidReportTestEncoder.WriteTouchPoint(
                    p,
                    DualSenseHidReportTestEncoder.PrimaryTouchPayloadOffset,
                    isActive: true,
                    trackingId: 6,
                    xNorm: 1f,
                    yNorm: 1f);
            });

        var stream = new FakeDualSenseHidStream([report]);
        var provider = new DualSenseHidInputProvider(streamFactory: new FakeDualSenseHidStreamFactory(stream));

        Assert.True(provider.TryGetState(out var state));
        Assert.InRange(state.PrimaryTouch.XNormalized, 0.998f, 1.001f);
        Assert.InRange(state.PrimaryTouch.YNormalized, 0.998f, 1.001f);
    }
}
