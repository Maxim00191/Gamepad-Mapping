#nullable enable

using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Support;

internal static class DualSenseHidTestReportFactory
{
    public static byte[] CreateReport(
        GamepadButtons buttons,
        byte reportId = 0x01,
        Action<Span<byte>>? customizePayload = null)
    {
        var report = new byte[64];
        report[0] = reportId;
        var payloadOffset = reportId == 0x31 ? 2 : 0;
        var payload = report.AsSpan(payloadOffset);
        if (payload.Length < 48)
            throw new InvalidOperationException("Test report too short for DualSense payload.");

        payload[1] = 128;
        payload[2] = 128;
        payload[3] = 128;
        payload[4] = 128;
        payload[8] = 0x08;

        ApplyButtons(payload, buttons);
        customizePayload?.Invoke(payload);

        return report;
    }

    public static void ApplyButtons(Span<byte> payload, GamepadButtons buttons)
    {
        var b0 = payload[8];
        var b1 = payload[9];

        b0 = (byte)((b0 & 0x0F) | 0x08);

        if (buttons.HasFlag(GamepadButtons.X))
            b0 |= 0b0001_0000;
        if (buttons.HasFlag(GamepadButtons.A))
            b0 |= 0b0010_0000;
        if (buttons.HasFlag(GamepadButtons.B))
            b0 |= 0b0100_0000;
        if (buttons.HasFlag(GamepadButtons.Y))
            b0 |= 0b1000_0000;

        b1 = 0;
        if (buttons.HasFlag(GamepadButtons.LeftShoulder))
            b1 |= 0b0000_0001;
        if (buttons.HasFlag(GamepadButtons.RightShoulder))
            b1 |= 0b0000_0010;
        if (buttons.HasFlag(GamepadButtons.LeftThumb))
            b1 |= 0b0000_0100;
        if (buttons.HasFlag(GamepadButtons.RightThumb))
            b1 |= 0b0000_1000;
        if (buttons.HasFlag(GamepadButtons.Back))
            b1 |= 0b0001_0000;
        if (buttons.HasFlag(GamepadButtons.Start))
            b1 |= 0b0010_0000;

        payload[8] = b0;
        payload[9] = b1;
    }

    public static void AssertNormalizedNear(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) < 0.002f, $"expected ~{expected}, actual {actual}");
}
