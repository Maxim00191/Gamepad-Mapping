using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using HidSharp;

namespace GamepadMapperGUI.Services.Input;

public sealed class DualSenseHidInputProvider : IPlayStationInputProvider
{
    private const int SonyVendorId = 0x054C;
    private static readonly ushort[] SupportedProductIds = [0x0CE6, 0x0DF2];

    private HidStream? _stream;
    private int _reportLength;

    public bool TryGetState(out PlayStationInputState state)
    {
        state = default;
        if (!EnsureOpenStream())
            return false;

        var stream = _stream;
        if (stream is null)
            return false;

        var buffer = new byte[_reportLength];
        try
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            ResetStream();
            return false;
        }
        catch (ObjectDisposedException)
        {
            ResetStream();
            return false;
        }

        var report = buffer.AsSpan();
        if (!TryGetPayloadSpan(report, out var payload))
            return false;

        var buttons = ParseButtons(payload);
        var leftThumb = new Vector2(NormalizeStick(payload[1]), -NormalizeStick(payload[2]));
        var rightThumb = new Vector2(NormalizeStick(payload[3]), -NormalizeStick(payload[4]));

        var gyro = new Vector3(
            BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(15, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(17, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(19, 2)));

        var touchPadPressed = (payload[10] & 0b0000_0010) != 0;
        var primaryTouch = ParseTouchPoint(payload, 33);
        var secondaryTouch = ParseTouchPoint(payload, 37);

        state = new PlayStationInputState(
            Buttons: buttons,
            LeftThumbstick: leftThumb,
            RightThumbstick: rightThumb,
            LeftTrigger: payload[5] / 255f,
            RightTrigger: payload[6] / 255f,
            Gyroscope: gyro,
            IsTouchpadPressed: touchPadPressed,
            PrimaryTouch: primaryTouch,
            SecondaryTouch: secondaryTouch,
            TimestampMs: Environment.TickCount64);
        return true;
    }

    private bool EnsureOpenStream()
    {
        if (_stream is not null)
            return true;

        foreach (var productId in SupportedProductIds)
        {
            var device = DeviceList.Local.GetHidDeviceOrNull(SonyVendorId, productId);
            if (device is null)
                continue;

            if (!device.TryOpen(out var stream))
                continue;

            stream.ReadTimeout = 5;
            _stream = stream;
            _reportLength = Math.Max(device.GetMaxInputReportLength(), 64);
            return true;
        }

        return false;
    }

    private static bool TryGetPayloadSpan(ReadOnlySpan<byte> report, out ReadOnlySpan<byte> payload)
    {
        payload = default;
        if (report.Length < 48)
            return false;

        var reportId = report[0];
        var offset = reportId switch
        {
            0x01 => 0,
            0x31 => 2,
            _ => -1
        };

        if (offset < 0 || report.Length < offset + 48)
            return false;

        payload = report.Slice(offset);
        return true;
    }

    private static GamepadButtons ParseButtons(ReadOnlySpan<byte> payload)
    {
        var buttons = GamepadButtons.None;
        var b0 = payload[8];
        var b1 = payload[9];

        buttons |= (b0 & 0b0001_0000) != 0 ? GamepadButtons.X : GamepadButtons.None;
        buttons |= (b0 & 0b0010_0000) != 0 ? GamepadButtons.A : GamepadButtons.None;
        buttons |= (b0 & 0b0100_0000) != 0 ? GamepadButtons.B : GamepadButtons.None;
        buttons |= (b0 & 0b1000_0000) != 0 ? GamepadButtons.Y : GamepadButtons.None;

        buttons |= (b1 & 0b0000_0001) != 0 ? GamepadButtons.LeftShoulder : GamepadButtons.None;
        buttons |= (b1 & 0b0000_0010) != 0 ? GamepadButtons.RightShoulder : GamepadButtons.None;
        buttons |= (b1 & 0b0000_0100) != 0 ? GamepadButtons.LeftThumb : GamepadButtons.None;
        buttons |= (b1 & 0b0000_1000) != 0 ? GamepadButtons.RightThumb : GamepadButtons.None;
        buttons |= (b1 & 0b0001_0000) != 0 ? GamepadButtons.Back : GamepadButtons.None;
        buttons |= (b1 & 0b0010_0000) != 0 ? GamepadButtons.Start : GamepadButtons.None;

        buttons |= (b0 & 0x0F) switch
        {
            0 => GamepadButtons.DPadUp,
            1 => GamepadButtons.DPadUp | GamepadButtons.DPadRight,
            2 => GamepadButtons.DPadRight,
            3 => GamepadButtons.DPadRight | GamepadButtons.DPadDown,
            4 => GamepadButtons.DPadDown,
            5 => GamepadButtons.DPadDown | GamepadButtons.DPadLeft,
            6 => GamepadButtons.DPadLeft,
            7 => GamepadButtons.DPadLeft | GamepadButtons.DPadUp,
            _ => GamepadButtons.None
        };

        return buttons;
    }

    private static PlayStationTouchPoint ParseTouchPoint(ReadOnlySpan<byte> payload, int startIndex)
    {
        if (payload.Length < startIndex + 4)
            return default;

        var counterAndActive = payload[startIndex];
        var isActive = (counterAndActive & 0x80) == 0;
        var trackingId = counterAndActive & 0x7F;

        var x = ((payload[startIndex + 2] & 0x0F) << 8) | payload[startIndex + 1];
        var y = (payload[startIndex + 3] << 4) | ((payload[startIndex + 2] & 0xF0) >> 4);

        return new PlayStationTouchPoint(
            IsActive: isActive,
            TrackingId: trackingId,
            XNormalized: Math.Clamp(x / 1919f, 0f, 1f),
            YNormalized: Math.Clamp(y / 1079f, 0f, 1f));
    }

    private static float NormalizeStick(byte value)
    {
        var normalized = (value - 127.5f) / 127.5f;
        return Math.Clamp(normalized, -1f, 1f);
    }

    private void ResetStream()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // ignored
        }

        _stream = null;
        _reportLength = 0;
    }
}
