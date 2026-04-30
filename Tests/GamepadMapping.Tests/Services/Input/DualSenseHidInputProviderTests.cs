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
            .Repeat(CreateReport(GamepadButtons.A), DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll)
            .Append(CreateReport(GamepadButtons.B));
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
    public void TryGetState_WhenDrainTimesOutAfterFirstReport_ReturnsFirstReportWithoutDisconnecting()
    {
        var stream = new FakeDualSenseHidStream([CreateReport(GamepadButtons.X)]);
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
        var stream = new FakeDualSenseHidStream([CreateReport(GamepadButtons.A)]);
        var provider = new DualSenseHidInputProvider(
            streamFactory: new FakeDualSenseHidStreamFactory(stream, opens: false));

        var ok = provider.TryGetState(out var state);

        Assert.False(ok);
        Assert.Equal(default, state);
        Assert.Equal(0, stream.ReadCount);
    }

    private static byte[] CreateReport(GamepadButtons buttons)
    {
        var report = new byte[64];
        report[0] = 0x01;
        report[1] = 128;
        report[2] = 128;
        report[3] = 128;
        report[4] = 128;
        report[8] = 0x08;

        if (buttons.HasFlag(GamepadButtons.X))
            report[8] |= 0b0001_0000;
        if (buttons.HasFlag(GamepadButtons.A))
            report[8] |= 0b0010_0000;
        if (buttons.HasFlag(GamepadButtons.B))
            report[8] |= 0b0100_0000;
        if (buttons.HasFlag(GamepadButtons.Y))
            report[8] |= 0b1000_0000;

        return report;
    }
}
