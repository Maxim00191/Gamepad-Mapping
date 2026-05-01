using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Input;

public sealed class DualSenseHidInputProvider : IPlayStationInputProvider
{
    private readonly ILogger? _logger;
    private readonly IDualSenseHidStreamFactory _streamFactory;
    private IDualSenseHidStream? _stream;
    private int _reportLength;
    private byte[]? _readBuffer;
    private byte[]? _drainBuffer;
    private long _lastHealthLogTick;
    private long _timeoutCount;
    private long _ioFailureCount;
    private long _openFailureCount;
    private long _streamResetCount;
    private long _drainedReportCount;
    private int _maxDrainedReportsPerPoll;

    public DualSenseHidInputProvider(
        ILogger? logger = null,
        IDualSenseHidStreamFactory? streamFactory = null)
    {
        _logger = logger;
        _streamFactory = streamFactory ?? new HidSharpDualSenseHidStreamFactory();
    }

    public bool TryGetState(out PlayStationInputState state)
    {
        state = default;
        if (!EnsureOpenStream())
            return false;

        var stream = _stream;
        if (stream is null)
            return false;

        var latestReport = _readBuffer;
        if (latestReport is null || latestReport.Length != _reportLength)
            return false;

        int read;
        int drainedReads = 0;
        try
        {
            read = stream.Read(latestReport, 0, latestReport.Length);
            if (read <= 0)
                return false;

            var drainBuffer = _drainBuffer ?? latestReport;
            var priorTimeout = stream.ReadTimeout;
            try
            {
                stream.ReadTimeout = DualSenseHidInputStreamConstraints.DrainReadTimeoutMs;
                while (drainedReads < DualSenseHidInputStreamConstraints.MaxDrainReadsPerPoll)
                {
                    var drainedRead = stream.Read(drainBuffer, 0, drainBuffer.Length);
                    if (drainedRead <= 0)
                        break;

                    Buffer.BlockCopy(drainBuffer, 0, latestReport, 0, drainedRead);
                    read = drainedRead;
                    drainedReads++;
                }
            }
            catch (TimeoutException)
            {
                // Expected when the queue is drained.
            }
            finally
            {
                stream.ReadTimeout = priorTimeout;
            }
        }
        catch (TimeoutException)
        {
            _timeoutCount++;
            TryLogHealthSnapshot();
            return false;
        }
        catch (IOException)
        {
            _ioFailureCount++;
            ResetStream();
            return false;
        }
        catch (ObjectDisposedException)
        {
            _ioFailureCount++;
            ResetStream();
            return false;
        }

        if (drainedReads > 0)
        {
            _drainedReportCount += drainedReads;
            _maxDrainedReportsPerPoll = Math.Max(_maxDrainedReportsPerPoll, drainedReads);
            TryLogHealthSnapshot();
        }

        var report = latestReport.AsSpan(0, read);
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

        if (!_streamFactory.TryOpen(out var stream, out var maxInputReportLength) || stream is null)
        {
            _openFailureCount++;
            TryLogHealthSnapshot();
            return false;
        }

        stream.ReadTimeout = DualSenseHidInputStreamConstraints.PrimaryReadTimeoutMs;
        _stream = stream;
        _reportLength = Math.Max(maxInputReportLength, 64);
        _readBuffer = new byte[_reportLength];
        _drainBuffer = new byte[_reportLength];
        TryLogHealthSnapshot(force: true);
        return true;
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
            XNormalized: Math.Clamp(x / DualSenseTouchpadGeometry.NormalizedWidthDivisor, 0f, 1f),
            YNormalized: Math.Clamp(y / DualSenseTouchpadGeometry.NormalizedHeightDivisor, 0f, 1f));
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
        _readBuffer = null;
        _drainBuffer = null;
        _streamResetCount++;
        TryLogHealthSnapshot(force: true);
    }

    private void TryLogHealthSnapshot(bool force = false)
    {
        if (_logger is null)
            return;

        var now = Environment.TickCount64;
        if (!force && _lastHealthLogTick != 0 && now - _lastHealthLogTick < DualSenseHidInputStreamConstraints.HealthLogIntervalMs)
            return;

        if (_timeoutCount == 0 && _ioFailureCount == 0 && _openFailureCount == 0 && _streamResetCount == 0 && _drainedReportCount == 0)
            return;

        _lastHealthLogTick = now;
        _logger.Info(
            $"DualSense HID health: timeouts={_timeoutCount}, ioFailures={_ioFailureCount}, openFailures={_openFailureCount}, resets={_streamResetCount}, drainedReports={_drainedReportCount}, maxDrainPerPoll={_maxDrainedReportsPerPoll}");
    }
}
