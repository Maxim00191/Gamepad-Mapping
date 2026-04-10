using System.Diagnostics;
using System.Numerics;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Interfaces;
using Vortice.XInput;

namespace GamepadMapperGUI.Core
{
    public class GamepadReader : IGamepadReader, IDisposable
    {
        private readonly IXInput _xinput;

        private Task? _pollingTask;
        private CancellationTokenSource? _cts;
        private int _pollingRateMs = 10;
        private InputFrame _previousFrame;
        private bool _hasPreviousState;
        private bool _isFirstFrameEmission;
        private readonly uint _userIndex = 0;
        private readonly object _startStopLock = new();
        private bool _disposed;

        private float _leftThumbstickDeadzone;
        private float _rightThumbstickDeadzone;

        private float _leftTriggerInnerDeadzone;
        private float _leftTriggerOuterDeadzone = 1f;
        private float _rightTriggerInnerDeadzone;
        private float _rightTriggerOuterDeadzone = 1f;

        private const float AnalogChangeEpsilon = 0.01f;
        private const float TriggerDeadzoneMinSpan = 0.02f;

        public event Action<InputFrame>? OnInputFrame;

        public GamepadReader(
            IXInput xinput,
            float? leftThumbstickDeadzone = null,
            float? rightThumbstickDeadzone = null,
            float? leftTriggerInnerDeadzone = null,
            float? leftTriggerOuterDeadzone = null,
            float? rightTriggerInnerDeadzone = null,
            float? rightTriggerOuterDeadzone = null)
        {
            _xinput = xinput;
            if (leftThumbstickDeadzone is { } left)
                LeftThumbstickDeadzone = left;
            if (rightThumbstickDeadzone is { } right)
                RightThumbstickDeadzone = right;
            if (leftTriggerInnerDeadzone is { } lti)
                LeftTriggerInnerDeadzone = lti;
            if (leftTriggerOuterDeadzone is { } lto)
                LeftTriggerOuterDeadzone = lto;
            if (rightTriggerInnerDeadzone is { } rti)
                RightTriggerInnerDeadzone = rti;
            if (rightTriggerOuterDeadzone is { } rto)
                RightTriggerOuterDeadzone = rto;
        }

        /// <summary>Left thumbstick deadzone in normalized [0..1] range, clamped internally.</summary>
        public float LeftThumbstickDeadzone
        {
            get => _leftThumbstickDeadzone;
            set => _leftThumbstickDeadzone = Math.Clamp(value, 0f, 0.9f);
        }

        /// <summary>Right thumbstick deadzone in normalized [0..1] range, clamped internally.</summary>
        public float RightThumbstickDeadzone
        {
            get => _rightThumbstickDeadzone;
            set => _rightThumbstickDeadzone = Math.Clamp(value, 0f, 0.9f);
        }

        /// <summary>Left trigger inner threshold [0..1]; values at or below map to 0.</summary>
        public float LeftTriggerInnerDeadzone
        {
            get => _leftTriggerInnerDeadzone;
            set
            {
                var v = Math.Clamp(value, 0f, 0.98f);
                _leftTriggerInnerDeadzone = v;
                if (_leftTriggerOuterDeadzone < v + TriggerDeadzoneMinSpan)
                    _leftTriggerOuterDeadzone = Math.Clamp(v + TriggerDeadzoneMinSpan, v + TriggerDeadzoneMinSpan, 1f);
            }
        }

        /// <summary>Left trigger outer threshold [inner+span..1]; values at or above map to 1.</summary>
        public float LeftTriggerOuterDeadzone
        {
            get => _leftTriggerOuterDeadzone;
            set => _leftTriggerOuterDeadzone = Math.Clamp(value, _leftTriggerInnerDeadzone + TriggerDeadzoneMinSpan, 1f);
        }

        /// <summary>Right trigger inner threshold (same semantics as <see cref="LeftTriggerInnerDeadzone"/>).</summary>
        public float RightTriggerInnerDeadzone
        {
            get => _rightTriggerInnerDeadzone;
            set
            {
                var v = Math.Clamp(value, 0f, 0.98f);
                _rightTriggerInnerDeadzone = v;
                if (_rightTriggerOuterDeadzone < v + TriggerDeadzoneMinSpan)
                    _rightTriggerOuterDeadzone = Math.Clamp(v + TriggerDeadzoneMinSpan, v + TriggerDeadzoneMinSpan, 1f);
            }
        }

        /// <summary>Right trigger outer threshold (same semantics as <see cref="LeftTriggerOuterDeadzone"/>).</summary>
        public float RightTriggerOuterDeadzone
        {
            get => _rightTriggerOuterDeadzone;
            set => _rightTriggerOuterDeadzone = Math.Clamp(value, _rightTriggerInnerDeadzone + TriggerDeadzoneMinSpan, 1f);
        }

        public void Start()
        {
            lock (_startStopLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GamepadReader));
                if (_cts != null && !_cts.IsCancellationRequested) return;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                _isFirstFrameEmission = true;
                _hasPreviousState = false;
                
                var token = _cts.Token;
                _pollingTask = Task.Run(() => PollingLoop(token), token);
                App.Logger.Info("Gamepad reader started.");
            }
        }

        public void Stop()
        {
            CancellationTokenSource? ctsToCancel = null;
            Task? taskToWait = null;

            lock (_startStopLock)
            {
                if (_cts == null || _cts.IsCancellationRequested) return;
                
                ctsToCancel = _cts;
                taskToWait = _pollingTask;
                _cts = null;
                _pollingTask = null;
            }

            ctsToCancel?.Cancel();
            try
            {
                taskToWait?.Wait(500);
            }
            catch (AggregateException) { } // Expected on cancellation
            finally
            {
                ctsToCancel?.Dispose();
            }
            App.Logger.Info("Gamepad reader stopped.");
        }

        public void Dispose()
        {
            lock (_startStopLock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            Stop();
        }

        private void PollingLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_xinput.GetState(_userIndex, out State xState))
                    {
                        var timestampMs = Environment.TickCount64;
                            var currentState = new InputFrame(
                                Buttons: (GamepadMapperGUI.Models.GamepadButtons)xState.Gamepad.Buttons,
                                LeftThumbstick: NormalizeThumbstick(xState.Gamepad.LeftThumbX, xState.Gamepad.LeftThumbY, _leftThumbstickDeadzone),
                                RightThumbstick: NormalizeThumbstick(xState.Gamepad.RightThumbX, xState.Gamepad.RightThumbY, _rightThumbstickDeadzone),
                                LeftTrigger: NormalizeTrigger(xState.Gamepad.LeftTrigger, _leftTriggerInnerDeadzone, _leftTriggerOuterDeadzone),
                                RightTrigger: NormalizeTrigger(xState.Gamepad.RightTrigger, _rightTriggerInnerDeadzone, _rightTriggerOuterDeadzone),
                                IsConnected: true,
                                TimestampMs: timestampMs);

                            var shouldEmit =
                                _isFirstFrameEmission ||
                                (_hasPreviousState && currentState.Buttons != _previousFrame.Buttons) ||
                                (_hasPreviousState && HasAnalogChanged(_previousFrame.LeftThumbstick, currentState.LeftThumbstick, AnalogChangeEpsilon)) ||
                                // Right stick: emit while engaged (steady aim) so mouse-look stays continuous; left stick is delta-only.
                                (_hasPreviousState &&
                                 (HasAnalogChanged(_previousFrame.RightThumbstick, currentState.RightThumbstick, AnalogChangeEpsilon) ||
                                  IsAnalogEngaged(currentState.RightThumbstick, AnalogChangeEpsilon))) ||
                                (_hasPreviousState &&
                                 HasAnalogChanged(_previousFrame.LeftTrigger, currentState.LeftTrigger, AnalogChangeEpsilon)) ||
                                (_hasPreviousState &&
                                 HasAnalogChanged(_previousFrame.RightTrigger, currentState.RightTrigger, AnalogChangeEpsilon));

                            if (shouldEmit)
                            {
                                OnInputFrame?.Invoke(currentState);
                            }

                            _previousFrame = currentState;
                            _hasPreviousState = true;
                            _isFirstFrameEmission = false;
                        }
                    else
                    {
                        if (_hasPreviousState)
                        {
                            OnInputFrame?.Invoke(InputFrame.Disconnected(Environment.TickCount64));

                            _hasPreviousState = false;
                            _isFirstFrameEmission = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Critical error in GamepadReader PollingLoop", ex);
                }

                if (ct.WaitHandle.WaitOne(_pollingRateMs))
                    break;
            }
        }

        private static float NormalizeTrigger(byte value, float inner, float outer)
        {
            var raw = value / 255f;
            if (raw <= inner) return 0f;
            if (raw >= outer) return 1f;
            return (raw - inner) / (outer - inner);
        }

        private static Vector2 NormalizeThumbstick(short x, short y, float deadzone)
        {
            var nx = ApplyDeadzone(NormalizeAxis(x), deadzone);
            var ny = ApplyDeadzone(NormalizeAxis(y), deadzone);
            return new Vector2(nx, ny);
        }

        private static float NormalizeAxis(short value)
        {
            // Handle the -32768 edge so we map to a clean [-1..1] range.
            var normalized = value < 0 ? value / 32768f : value / 32767f;
            if (normalized > 1f) return 1f;
            if (normalized < -1f) return -1f;
            return normalized;
        }

        private static float ApplyDeadzone(float v, float deadzone)
        {
            var av = MathF.Abs(v);
            if (av < deadzone) return 0f;

            var sign = MathF.Sign(v);
            return sign * (av - deadzone) / (1f - deadzone);
        }

        private static bool HasAnalogChanged(float previousValue, float currentValue, float epsilon)
            => MathF.Abs(currentValue - previousValue) > epsilon;

        private static bool HasAnalogChanged(Vector2 previousValue, Vector2 currentValue, float epsilon)
            => Vector2.DistanceSquared(previousValue, currentValue) > epsilon * epsilon;

        private static bool IsAnalogEngaged(Vector2 value, float epsilon)
            => value.LengthSquared() > epsilon * epsilon;
    }
}
