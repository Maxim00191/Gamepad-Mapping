using System.Diagnostics;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Vortice.XInput;

namespace GamepadMapperGUI.Core
{
    public class GamepadReader : IGamepadReader
    {
        private bool _isRunning;
        private int _pollingRateMs = 10;
        private State _previousState;
        private bool _hasPreviousState;
        private bool _isFirstFrameEmission;
        private readonly uint _userIndex = 0;

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
            float? leftThumbstickDeadzone = null,
            float? rightThumbstickDeadzone = null,
            float? leftTriggerInnerDeadzone = null,
            float? leftTriggerOuterDeadzone = null,
            float? rightTriggerInnerDeadzone = null,
            float? rightTriggerOuterDeadzone = null)
        {
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
            if (_isRunning) return;
            _isRunning = true;

            _hasPreviousState = XInput.GetState(_userIndex, out _previousState);
            _isFirstFrameEmission = true;
            Task.Run(() => PollingLoop());
            Debug.WriteLine("Gamepad reader started.");
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            Debug.WriteLine("Gamepad reader stopped.");
        }

        private void PollingLoop()
        {
            while (_isRunning)
            {
                if (XInput.GetState(_userIndex, out State currentState))
                {
                    var currentButtons = currentState.Gamepad.Buttons;
                    var timestampMs = Environment.TickCount64;

                    var currentLeftThumb = NormalizeThumbstick(currentState.Gamepad.LeftThumbX, currentState.Gamepad.LeftThumbY, _leftThumbstickDeadzone);
                    var currentRightThumb = NormalizeThumbstick(currentState.Gamepad.RightThumbX, currentState.Gamepad.RightThumbY, _rightThumbstickDeadzone);
                    var currentLeftTrigger = NormalizeTrigger(currentState.Gamepad.LeftTrigger, _leftTriggerInnerDeadzone, _leftTriggerOuterDeadzone);
                    var currentRightTrigger = NormalizeTrigger(currentState.Gamepad.RightTrigger, _rightTriggerInnerDeadzone, _rightTriggerOuterDeadzone);

                    var shouldEmit =
                        _isFirstFrameEmission ||
                        (_hasPreviousState && currentButtons != _previousState.Gamepad.Buttons) ||
                        (_hasPreviousState && HasAnalogChanged(NormalizeThumbstick(_previousState.Gamepad.LeftThumbX, _previousState.Gamepad.LeftThumbY, _leftThumbstickDeadzone), currentLeftThumb, AnalogChangeEpsilon)) ||
                        (_hasPreviousState &&
                         (HasAnalogChanged(NormalizeThumbstick(_previousState.Gamepad.RightThumbX, _previousState.Gamepad.RightThumbY, _rightThumbstickDeadzone), currentRightThumb, AnalogChangeEpsilon) ||
                          IsAnalogEngaged(currentRightThumb, AnalogChangeEpsilon))) ||
                        (_hasPreviousState &&
                         HasAnalogChanged(
                             NormalizeTrigger(_previousState.Gamepad.LeftTrigger, _leftTriggerInnerDeadzone, _leftTriggerOuterDeadzone),
                             currentLeftTrigger,
                             AnalogChangeEpsilon)) ||
                        (_hasPreviousState &&
                         HasAnalogChanged(
                             NormalizeTrigger(_previousState.Gamepad.RightTrigger, _rightTriggerInnerDeadzone, _rightTriggerOuterDeadzone),
                             currentRightTrigger,
                             AnalogChangeEpsilon));

                    if (shouldEmit)
                    {
                        OnInputFrame?.Invoke(new InputFrame(
                            Buttons: currentButtons,
                            LeftThumbstick: currentLeftThumb,
                            RightThumbstick: currentRightThumb,
                            LeftTrigger: currentLeftTrigger,
                            RightTrigger: currentRightTrigger,
                            IsConnected: true,
                            TimestampMs: timestampMs));
                    }

                    _previousState = currentState;
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

                Thread.Sleep(_pollingRateMs);
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
