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

        private const float ThumbstickDeadzone = 0.10f;

        private const float AnalogChangeEpsilon = 0.01f;

        public event Action<InputFrame>? OnInputFrame;

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

                    var currentLeftThumb = NormalizeThumbstick(currentState.Gamepad.LeftThumbX, currentState.Gamepad.LeftThumbY);
                    var currentRightThumb = NormalizeThumbstick(currentState.Gamepad.RightThumbX, currentState.Gamepad.RightThumbY);
                    var currentLeftTrigger = NormalizeTrigger(currentState.Gamepad.LeftTrigger);
                    var currentRightTrigger = NormalizeTrigger(currentState.Gamepad.RightTrigger);

                    var shouldEmit =
                        _isFirstFrameEmission ||
                        (_hasPreviousState && currentButtons != _previousState.Gamepad.Buttons) ||
                        (_hasPreviousState && HasAnalogChanged(NormalizeThumbstick(_previousState.Gamepad.LeftThumbX, _previousState.Gamepad.LeftThumbY), currentLeftThumb, AnalogChangeEpsilon)) ||
                        (_hasPreviousState &&
                         (HasAnalogChanged(NormalizeThumbstick(_previousState.Gamepad.RightThumbX, _previousState.Gamepad.RightThumbY), currentRightThumb, AnalogChangeEpsilon) ||
                          IsAnalogEngaged(currentRightThumb, AnalogChangeEpsilon))) ||
                        (_hasPreviousState && HasAnalogChanged(NormalizeTrigger(_previousState.Gamepad.LeftTrigger), currentLeftTrigger, AnalogChangeEpsilon)) ||
                        (_hasPreviousState && HasAnalogChanged(NormalizeTrigger(_previousState.Gamepad.RightTrigger), currentRightTrigger, AnalogChangeEpsilon));

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

        private static float NormalizeTrigger(byte value) => value / 255f;

        private static Vector2 NormalizeThumbstick(short x, short y)
        {
            var nx = ApplyDeadzone(NormalizeAxis(x));
            var ny = ApplyDeadzone(NormalizeAxis(y));
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

        private static float ApplyDeadzone(float v)
        {
            var av = MathF.Abs(v);
            if (av < ThumbstickDeadzone) return 0f;

            var sign = MathF.Sign(v);
            return sign * (av - ThumbstickDeadzone) / (1f - ThumbstickDeadzone);
        }

        private static bool HasAnalogChanged(float previousValue, float currentValue, float epsilon)
            => MathF.Abs(currentValue - previousValue) > epsilon;

        private static bool HasAnalogChanged(Vector2 previousValue, Vector2 currentValue, float epsilon)
            => Vector2.DistanceSquared(previousValue, currentValue) > epsilon * epsilon;

        private static bool IsAnalogEngaged(Vector2 value, float epsilon)
            => value.LengthSquared() > epsilon * epsilon;
    }
}
