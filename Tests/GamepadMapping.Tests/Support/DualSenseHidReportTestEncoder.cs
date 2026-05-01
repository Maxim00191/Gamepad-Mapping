#nullable enable

using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Support;

internal static class DualSenseHidReportTestEncoder
{
    public const int PrimaryTouchPayloadOffset = 33;

    public const int SecondaryTouchPayloadOffset = 37;

    public static void WriteTouchPoint(Span<byte> payload, int startIndex, bool isActive, int trackingId, float xNorm, float yNorm)
    {
        var x = (int)Math.Clamp(
            Math.Round(xNorm * DualSenseTouchpadGeometry.NormalizedWidthDivisor),
            0,
            DualSenseTouchpadGeometry.MaxRawX);
        var y = (int)Math.Clamp(
            Math.Round(yNorm * DualSenseTouchpadGeometry.NormalizedHeightDivisor),
            0,
            DualSenseTouchpadGeometry.MaxRawY);

        var counter = (byte)((trackingId & 0x7F) | (isActive ? 0 : 0x80));
        var b1 = (byte)(x & 0xFF);
        var xh = (x >> 8) & 0x0F;
        var yLow = y & 0x0F;
        var b2 = (byte)(xh | (yLow << 4));
        var b3 = (byte)(y >> 4);

        if (payload.Length < startIndex + 4)
            return;

        payload[startIndex] = counter;
        payload[startIndex + 1] = b1;
        payload[startIndex + 2] = b2;
        payload[startIndex + 3] = b3;
    }

    public static void WriteTouchpadClick(Span<byte> payload, bool pressed)
    {
        if (payload.Length <= 10)
            return;

        if (pressed)
            payload[10] |= 0b0000_0010;
        else
            payload[10] &= unchecked((byte)~0b0000_0010);
    }
}
