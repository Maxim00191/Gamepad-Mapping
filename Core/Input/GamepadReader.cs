using System.Diagnostics;
using System.Numerics;
using GamepadMapperGUI.Interfaces.Core;
using Vortice.XInput;

namespace GamepadMapperGUI.Core
{
    public class GamepadReader : IGamepadReader
    {
        private bool _isRunning;
        private int _pollingRateMs = 10;
        private State _previousState;
        private bool _hasPreviousState;
        private readonly uint _userIndex = 0;

        private const float ThumbstickDeadzone = 0.10f;

        private const float AnalogChangeEpsilon = 0.01f;

        public event Action<GamepadButtons>? OnButtonPressed;
        public event Action<GamepadButtons>? OnButtonReleased;
        public event Action<Vector2>? OnLeftThumbstickChanged;
        public event Action<Vector2>? OnRightThumbstickChanged;
        public event Action<float>? OnLeftTriggerChanged;
        public event Action<float>? OnRightTriggerChanged;

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _hasPreviousState = XInput.GetState(_userIndex, out _previousState);
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
                    var previousButtons = _previousState.Gamepad.Buttons;

                    if (_hasPreviousState && currentButtons != previousButtons)
                    {
                        var pressedMask = currentButtons & ~previousButtons;
                        foreach (var pressed in GetIndividualButtonFlags(pressedMask))
                        {
                            OnButtonPressed?.Invoke(pressed);
                        }

                        var releasedMask = previousButtons & ~currentButtons;
                        foreach (var released in GetIndividualButtonFlags(releasedMask))
                        {
                            OnButtonReleased?.Invoke(released);
                        }
                    }

                    if (_hasPreviousState)
                    {
                        var currentLeftThumb = NormalizeThumbstick(currentState.Gamepad.LeftThumbX, currentState.Gamepad.LeftThumbY);
                        var previousLeftThumb = NormalizeThumbstick(_previousState.Gamepad.LeftThumbX, _previousState.Gamepad.LeftThumbY);
                        if (HasAnalogChanged(previousLeftThumb, currentLeftThumb, AnalogChangeEpsilon))
                        {
                            OnLeftThumbstickChanged?.Invoke(currentLeftThumb);
                        }

                        var currentRightThumb = NormalizeThumbstick(currentState.Gamepad.RightThumbX, currentState.Gamepad.RightThumbY);
                        var previousRightThumb = NormalizeThumbstick(_previousState.Gamepad.RightThumbX, _previousState.Gamepad.RightThumbY);
                        // Keep publishing right-stick input while engaged so view-rotation mappings
                        // can continue producing relative mouse movement even when value is stable.
                        if (HasAnalogChanged(previousRightThumb, currentRightThumb, AnalogChangeEpsilon) ||
                            IsAnalogEngaged(currentRightThumb, AnalogChangeEpsilon))
                        {
                            OnRightThumbstickChanged?.Invoke(currentRightThumb);
                        }

                        var currentLeftTrigger = NormalizeTrigger(currentState.Gamepad.LeftTrigger);
                        var previousLeftTrigger = NormalizeTrigger(_previousState.Gamepad.LeftTrigger);
                        if (HasAnalogChanged(previousLeftTrigger, currentLeftTrigger, AnalogChangeEpsilon))
                        {
                            OnLeftTriggerChanged?.Invoke(currentLeftTrigger);
                        }

                        var currentRightTrigger = NormalizeTrigger(currentState.Gamepad.RightTrigger);
                        var previousRightTrigger = NormalizeTrigger(_previousState.Gamepad.RightTrigger);
                        if (HasAnalogChanged(previousRightTrigger, currentRightTrigger, AnalogChangeEpsilon))
                        {
                            OnRightTriggerChanged?.Invoke(currentRightTrigger);
                        }
                    }

                    _previousState = currentState;
                    _hasPreviousState = true;
                }
                else
                {
                    // If we previously had a state and now we don't, proactively "release" anything that was pressed
                    // so consumers don't get stuck with phantom button-down events.
                    if (_hasPreviousState)
                    {
                        foreach (var released in GetIndividualButtonFlags(_previousState.Gamepad.Buttons))
                        {
                            OnButtonReleased?.Invoke(released);
                        }

                        var prevLeftThumb = NormalizeThumbstick(_previousState.Gamepad.LeftThumbX, _previousState.Gamepad.LeftThumbY);
                        if (HasAnalogChanged(prevLeftThumb, Vector2.Zero, AnalogChangeEpsilon))
                        {
                            OnLeftThumbstickChanged?.Invoke(Vector2.Zero);
                        }

                        var prevRightThumb = NormalizeThumbstick(_previousState.Gamepad.RightThumbX, _previousState.Gamepad.RightThumbY);
                        if (HasAnalogChanged(prevRightThumb, Vector2.Zero, AnalogChangeEpsilon))
                        {
                            OnRightThumbstickChanged?.Invoke(Vector2.Zero);
                        }

                        var prevLeftTrigger = NormalizeTrigger(_previousState.Gamepad.LeftTrigger);
                        if (HasAnalogChanged(prevLeftTrigger, 0f, AnalogChangeEpsilon))
                        {
                            OnLeftTriggerChanged?.Invoke(0f);
                        }

                        var prevRightTrigger = NormalizeTrigger(_previousState.Gamepad.RightTrigger);
                        if (HasAnalogChanged(prevRightTrigger, 0f, AnalogChangeEpsilon))
                        {
                            OnRightTriggerChanged?.Invoke(0f);
                        }

                        _hasPreviousState = false;
                    }
                }

                Thread.Sleep(_pollingRateMs);
            }
        }

        private static IEnumerable<GamepadButtons> GetIndividualButtonFlags(GamepadButtons buttons)
        {
            var mask = (uint)buttons;
            if (mask == 0) yield break;

            // GamepadButtons is a flags enum; emit each set bit as a single button.
            for (var bitIndex = 0; bitIndex < 32; bitIndex++)
            {
                var bit = 1u << bitIndex;
                if ((mask & bit) != 0)
                {
                    var flag = (GamepadButtons)bit;
                    if (Enum.IsDefined(typeof(GamepadButtons), flag))
                    {
                        yield return flag;
                    }
                }
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
