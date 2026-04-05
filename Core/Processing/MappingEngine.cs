using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

public sealed class MappingEngine : IMappingEngine
{
    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutputLive;
    private bool _processingInputFrame;
    private bool _frameDispatchAllowed;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly OutputStateTracker _outputStateTracker = new();
    private readonly AnalogProcessor _analogProcessor = new();
    private readonly InputDispatcher _inputDispatcher;
    private readonly Action<ComboHudContent?>? _setComboHud;
    private readonly object _inputFrameSync = new();
    private readonly HoldSessionManager _holdSessionManager;
    private readonly InputFramePipeline _inputFramePipeline;
    private readonly ButtonEventPipeline _buttonEventPipeline;
    private readonly ItemCycleProcessor _itemCycleProcessor = new();

    private readonly int _comboHudDelayMs;
    private readonly int _leadKeyReleaseSuppressMs;

    private readonly TriggerMoment _buttonPressedTrigger = TriggerMoment.Pressed;
    private readonly TriggerMoment _buttonTapTrigger = TriggerMoment.Tap;

    private readonly Action<string>? _requestTemplateSwitchToProfileId;

    private IReadOnlyCollection<GamepadButtons> _latestActiveButtons = Array.Empty<GamepadButtons>();
    private float _latestLeftTrigger;
    private float _latestRightTrigger;
    private IReadOnlyList<MappingEntry> _lastButtonMappingsSnapshot = Array.Empty<MappingEntry>();

    /// <summary>Buttons whose solo <see cref="TriggerMoment.Pressed"/> was deferred because the profile has a richer chord using them.</summary>
    private readonly HashSet<GamepadButtons> _deferredSoloLeadButtons = [];
    private readonly object _deferredSoloLeadButtonsLock = new();

    /// <summary>Non-null = exact combo leads from profile JSON; null = infer from mappings each event.</summary>
    private HashSet<GamepadButtons>? _explicitComboLeadsFromTemplate;

    private List<RadialMenuDefinition>? _radialMenusPersist;
    private List<KeyboardActionDefinition>? _keyboardActionsPersist;

    private readonly AnalogMappingProcessor _analogMappingProcessor;
    private readonly ComboHudManager? _comboHudManager;
    private readonly ButtonMappingProcessor _buttonMappingProcessor;
    private readonly ITimeProvider _timeProvider;
    private readonly Action<Action> _runOnUi;
    private readonly IRadialMenuHud _radialMenuHud;
    private readonly Func<float> _getRadialMenuStickEngagementThreshold;
    private readonly Func<RadialMenuConfirmMode> _getRadialMenuConfirmMode;

    private HashSet<GamepadButtons>? _radialChordSuppressionButtons;
    private bool _radialPrevStickEngaged;
    private bool _radialStickEverEngagedWhileOpen;
    private int _radialLastSectorWhileEngaged = -1;

    public MappingEngine(
        IKeyboardEmulator keyboardEmulator,
        IMouseEmulator mouseEmulator,
        Func<bool> canDispatchOutputLive,
        Action<Action> runOnUi,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<ComboHudContent?>? setComboHud = null,
        int modifierGraceMs = HoldSessionManager.DefaultModifierGraceMs,
        int leadKeyReleaseSuppressMs = 500,
        Action<string>? requestTemplateSwitchToProfileId = null,
        Action<string?>? setComboHudGateHint = null,
        Func<string>? comboHudGateMessageFactory = null,
        Func<bool>? isComboHudPresentationSuppressed = null,
        IRadialMenuHud? radialMenuHud = null,
        Func<float>? getRadialMenuStickEngagementThreshold = null,
        Func<RadialMenuConfirmMode>? getRadialMenuConfirmMode = null,
        ITimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? new RealTimeProvider();
        _runOnUi = runOnUi;
        _radialMenuHud = radialMenuHud ?? NullRadialMenuHud.Instance;
        _getRadialMenuStickEngagementThreshold =
            getRadialMenuStickEngagementThreshold ?? (() => 0.35f);
        _getRadialMenuConfirmMode =
            getRadialMenuConfirmMode ?? (() => RadialMenuConfirmMode.ReleaseGuideKey);
        _comboHudDelayMs = modifierGraceMs;
        _leadKeyReleaseSuppressMs = leadKeyReleaseSuppressMs;
        _requestTemplateSwitchToProfileId = requestTemplateSwitchToProfileId;
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _canDispatchOutputLive = canDispatchOutputLive;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _setComboHud = setComboHud;
        _inputDispatcher = new InputDispatcher(
            DispatchMappedOutputAsync,
            (modifiers, mainKey, ct) => _keyboardEmulator.TapKeyChordAsync(modifiers, mainKey, cancellationToken: ct),
            runOnUi,
            setMappedOutput,
            setMappingStatus);
        _holdSessionManager = new HoldSessionManager(
            () => CanDispatchOutputMerged(),
            _setMappedOutput,
            _setMappingStatus,
            QueueOutputDispatch,
            () => _comboHudManager?.Sync(),
            mappings => ResolveComboLeads(mappings),
            _timeProvider,
            modifierGraceMs,
            leadKeyReleaseSuppressMs,
            runSynchronizedWithInputFrame: action =>
            {
                lock (_inputFrameSync)
                    action();
            });

        _analogMappingProcessor = new AnalogMappingProcessor(
            _analogProcessor,
            _keyboardEmulator,
            _mouseEmulator,
            () => CanDispatchOutputMerged(),
            _setMappedOutput,
            _setMappingStatus);

        if (_setComboHud != null)
        {
            _comboHudManager = new ComboHudManager(
                _setComboHud,
                () => CanDispatchOutputMerged(),
                () => _lastButtonMappingsSnapshot,
                () => _latestActiveButtons,
                mappings => ResolveComboLeads(mappings),
                _holdSessionManager,
                _comboHudDelayMs,
                setComboHudGateHint,
                comboHudGateMessageFactory,
                isComboHudPresentationSuppressed);
        }

        _buttonMappingProcessor = new ButtonMappingProcessor(
        _holdSessionManager,
        _outputStateTracker,
        _itemCycleProcessor,
        () => CanDispatchOutputMerged(),
        _setMappedOutput,
        _setMappingStatus,
        QueueOutputDispatch,
        EnqueueItemCycleTap,
        TryDispatchTemplateToggle,
        TryDispatchRadialMenu,
        mappings => ResolveComboLeads(mappings),
        _leadKeyReleaseSuppressMs,
        _deferredSoloLeadButtons,
        _deferredSoloLeadButtonsLock,
        shouldSuppressAllMappingForEvent: SuppressRadialChordButtonMapping);

        _inputFramePipeline = new InputFramePipeline(
            middlewares: new IInputFrameMiddleware[] { new InputFrameTransitionMiddleware() },
            terminal: ProcessInputFrameTerminal);

        _buttonEventPipeline = new ButtonEventPipeline(
            middlewares:
            [
                new ButtonEventPreparationMiddleware(
                    setLatestInputState: (activeButtons, leftTrigger, rightTrigger) =>
                    {
                        _latestLeftTrigger = leftTrigger;
                        _latestRightTrigger = rightTrigger;
                        _holdSessionManager.UpdateLatestInputState(activeButtons, leftTrigger, rightTrigger);
                    },
                    registerButtonPressed: _holdSessionManager.RegisterButtonPressed,
                    registerButtonReleased: _holdSessionManager.RegisterButtonReleased,
                    cancelSupersededHoldSessions: _holdSessionManager.CancelHoldSessionsSupersededByMoreSpecificChord,
                    handleHoldRelease: (btn, active, lt, rt, heldMs) => _holdSessionManager.HandleHoldBindingRelease(
                        btn,
                        active,
                        lt,
                        rt,
                        heldMs,
                        ResolveComboLeads(_lastButtonMappingsSnapshot).Contains(btn)),
                    getReleasedButtonHeldMs: btn => _holdSessionManager.TryGetPressedDurationMs(btn),
                    forceReleaseHeldOutputsForButton: ForceReleaseHeldOutputsForButton,
                    collectReleasedOutputsHandledByMappings: (btn, active, snap, lt, rt, heldMs) => _buttonMappingProcessor.CollectReleasedOutputsHandledByMappings(btn, active, snap, lt, rt, heldMs),
                    setLatestActiveButtons: activeButtons => _latestActiveButtons = activeButtons,
                    canDispatchOutput: () => CanDispatchOutputMerged(),
                    setMappingStatus: _setMappingStatus)
            ],
            terminal: _buttonMappingProcessor.ProcessButtonEventTerminal);
    }

    /// <inheritdoc />
    public void SetComboLeadButtonsFromTemplate(IReadOnlyList<string>? comboLeadButtonNames) =>
        _explicitComboLeadsFromTemplate = ComboLeadSemantics.ParseDeclaredNames(comboLeadButtonNames);

    public void SetRadialMenuDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions)
    {
        _radialMenusPersist = radialMenus;
        _keyboardActionsPersist = keyboardActions;
    }

    private HashSet<GamepadButtons> ResolveComboLeads(IReadOnlyCollection<MappingEntry> mappings) =>
        ComboLeadSemantics.ResolveLeads(mappings, _explicitComboLeadsFromTemplate);

    private bool CanDispatchOutputMerged() =>
        _processingInputFrame ? _frameDispatchAllowed : _canDispatchOutputLive();

    public InputFrameProcessingResult ProcessInputFrame(InputFrame frame, IReadOnlyList<MappingEntry> mappingsSnapshot, bool canDispatchMappedOutput = true)
    {
        lock (_inputFrameSync)
        {
            _processingInputFrame = true;
            _frameDispatchAllowed = canDispatchMappedOutput;
            try
            {
                _lastButtonMappingsSnapshot = mappingsSnapshot;

                var context = new InputFrameContext
                {
                    Frame = frame
                };

                _inputFramePipeline.Invoke(context);

                return new InputFrameProcessingResult(
                    context.PressedButtons.ToArray(),
                    context.ReleasedButtons.ToArray());
            }
            finally
            {
                _processingInputFrame = false;
            }
        }
    }

    private void ProcessInputFrameTerminal(InputFrameContext context)
    {
        var frame = context.Frame;

        _latestLeftTrigger = frame.LeftTrigger;
        _latestRightTrigger = frame.RightTrigger;

        var activeButtonsNow = ToActiveButtonsSet(frame.Buttons);
        _latestActiveButtons = activeButtonsNow;
        _holdSessionManager.UpdateLatestInputState(_latestActiveButtons, frame.LeftTrigger, frame.RightTrigger);

        // Update active radial menu selection if one is open
        UpdateRadialMenuSelection(frame);

        // The first frame is treated as an initialization frame; button transitions are ignored.
        if (!context.IsFirstFrame)
        {
            var workingActiveButtons = ToActiveButtonsSet(context.PreviousButtonsMask);

            foreach (var pressedButton in context.PressedButtons)
            {
                workingActiveButtons.Add(pressedButton);

                // Keep the legacy ordering: Pressed mappings first, then Tap mappings.
                HandleButtonMappingsInternal(
                    pressedButton,
                    _buttonPressedTrigger,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);

                HandleButtonMappingsInternal(
                    pressedButton,
                    _buttonTapTrigger,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);
            }

            foreach (var releasedButton in context.ReleasedButtons)
            {
                HandleButtonMappingsInternal(
                    releasedButton,
                    TriggerMoment.Released,
                    workingActiveButtons,
                    _lastButtonMappingsSnapshot,
                    frame.LeftTrigger,
                    frame.RightTrigger);

                TryFinishRadialMenuOnChordRelease(releasedButton);

                workingActiveButtons.Remove(releasedButton);
            }
        }

        // Continuous analog evaluation; the stick used for radial selection reads real deflection in UpdateRadialMenuSelection only.
        var leftAnalog = ThumbstickValueForAnalogWhileRadialOpen(_activeRadialMenuDefinition, GamepadBindingType.LeftThumbstick, frame.LeftThumbstick);
        var rightAnalog = ThumbstickValueForAnalogWhileRadialOpen(_activeRadialMenuDefinition, GamepadBindingType.RightThumbstick, frame.RightThumbstick);
        _analogMappingProcessor.ProcessThumbstick(GamepadBindingType.LeftThumbstick, leftAnalog, _lastButtonMappingsSnapshot);
        _analogMappingProcessor.ProcessThumbstick(GamepadBindingType.RightThumbstick, rightAnalog, _lastButtonMappingsSnapshot);
        _analogMappingProcessor.ProcessTrigger(GamepadBindingType.LeftTrigger, frame.LeftTrigger, _lastButtonMappingsSnapshot, SendPointerAction);
        _analogMappingProcessor.ProcessTrigger(GamepadBindingType.RightTrigger, frame.RightTrigger, _lastButtonMappingsSnapshot, SendPointerAction);

        TrySyncComboHud(context);
    }

    private void TrySyncComboHud(InputFrameContext context)
    {
        if (_comboHudManager is null)
            return;

        var frameHasButtonEdges = context.IsFirstFrame ||
            context.PressedButtons.Length > 0 ||
            context.ReleasedButtons.Length > 0;

        if (frameHasButtonEdges || _comboHudManager.AwaitingComboHudDelay)
            _comboHudManager.Sync();
    }

    private static HashSet<GamepadButtons> ToActiveButtonsSet(GamepadButtons buttons)
    {
        var result = new HashSet<GamepadButtons>();
        var mask = (uint)buttons;
        if (mask == 0) return result;

        for (var bitIndex = 0; bitIndex < 32; bitIndex++)
        {
            var bit = 1u << bitIndex;
            if ((mask & bit) == 0) continue;

            var flag = (GamepadButtons)bit;
            if (Enum.IsDefined(typeof(GamepadButtons), flag))
                result.Add(flag);
        }

        return result;
    }

    private void HandleButtonMappingsInternal(
        GamepadButtons buttons,
        TriggerMoment trigger,
        IReadOnlyCollection<GamepadButtons> activeButtons,
        IReadOnlyList<MappingEntry> mappingsSnapshot,
        float leftTriggerValue,
        float rightTriggerValue)
    {
        var context = new ButtonEventContext
        {
            Button = buttons,
            Trigger = trigger,
            ActiveButtons = activeButtons,
            MappingsSnapshot = mappingsSnapshot,
            LeftTriggerValue = leftTriggerValue,
            RightTriggerValue = rightTriggerValue
        };

        _buttonEventPipeline.Invoke(context);
    }

    public void ForceReleaseAllOutputs()
    {
        lock (_inputFrameSync)
        {
            lock (_deferredSoloLeadButtonsLock)
            {
                _latestActiveButtons = Array.Empty<GamepadButtons>();
                _deferredSoloLeadButtons.Clear();
                _itemCycleProcessor.Reset();
                _holdSessionManager.ClearButtonDownTicks();
                _holdSessionManager.ClearAllHoldSessions();
                _outputStateTracker.ForceReleaseAllOutputs(ForceReleaseOutput);
                _analogMappingProcessor.ForceReleaseAnalogOutputs();
                _radialChordSuppressionButtons = null;
                AbandonRadialMenuSessionForForceReset();
            }
        }
    }

    public void ForceReleaseAnalogOutputs()
    {
        _analogMappingProcessor.ForceReleaseAnalogOutputs();
    }

    /// <inheritdoc />
    public void RefreshComboHud()
    {
        lock (_inputFrameSync)
            _comboHudManager?.Sync();
    }

    /// <inheritdoc />
    public void InvalidateComboHudPresentation()
    {
        lock (_inputFrameSync)
            _comboHudManager?.InvalidateLastPresentedSignature();
    }

    /// <inheritdoc />
    public Task WaitForIdleAsync() => _inputDispatcher.WaitForIdleAsync();

    public void Dispose()
    {
        try
        {
            _comboHudManager?.Dispose();
            ForceReleaseAllOutputs();
            ForceReleaseAnalogOutputs();
            _inputDispatcher.Dispose();
            _radialMenuHud.Dispose();
        }
        catch
        {
            // Best-effort shutdown.
        }
    }

    public static Key ParseKey(string? keyboardKey) => InputTokenResolver.ParseKey(keyboardKey);

    public static string NormalizeKeyboardKeyToken(string keyboardKey) => InputTokenResolver.NormalizeKeyboardKeyToken(keyboardKey);

    public static bool IsMouseLookOutput(string token) => AnalogProcessor.IsMouseLookOutput(token);

    /// <summary>Whether <paramref name="token"/> can be stored as a mapping output (keyboard, pointer alias, or mouse-look axis).</summary>
    public static bool TryNormalizeMappedOutputStorage(string? token, out string stored)
    {
        stored = string.Empty;
        var raw = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (IsMouseLookOutput(raw))
        {
            stored = NormalizeKeyboardKeyToken(raw);
            return true;
        }

        var key = ParseKey(raw);
        if (key != Key.None)
        {
            stored = key.ToString();
            return true;
        }

        if (InputTokenResolver.TryResolveMappedOutput(raw, out _, out _))
        {
            stored = NormalizeKeyboardKeyToken(raw);
            return true;
        }

        return false;
    }

    private async Task DispatchMappedOutputAsync(DispatchedOutput output, TriggerMoment trigger, CancellationToken cancellationToken)
    {
        if (output.KeyboardKey is Key key && key != Key.None)
        {
            if (trigger == TriggerMoment.Pressed)
                _keyboardEmulator.KeyDown(key);
            else if (trigger == TriggerMoment.Released)
                _keyboardEmulator.KeyUp(key);
            else
                await _keyboardEmulator.TapKeyAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (output.PointerAction is PointerAction pointerAction)
            await SendPointerActionAsync(pointerAction, trigger, cancellationToken).ConfigureAwait(false);
    }

    private void ForceReleaseHeldOutputsForButton(GamepadButtons button, IReadOnlySet<DispatchedOutput>? outputsHandledByReleasedMappings = null)
    {
        _outputStateTracker.ForceReleaseHeldOutputsForButton(button, ForceReleaseOutput, outputsHandledByReleasedMappings);
    }

    private void ForceReleaseOutput(DispatchedOutput output)
    {
        QueueOutputDispatch("ForceRelease", TriggerMoment.Released, output, "Forced release", "forced-release");
    }

    private void SendPointerAction(PointerAction action, TriggerMoment trigger) =>
        SendPointerActionAsync(action, trigger, CancellationToken.None).GetAwaiter().GetResult();

    private async Task SendPointerActionAsync(PointerAction action, TriggerMoment trigger, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case PointerAction.LeftClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.LeftDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.LeftUp();
                else await _mouseEmulator.LeftClickAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PointerAction.RightClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.RightDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.RightUp();
                else await _mouseEmulator.RightClickAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PointerAction.MiddleClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.MiddleDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.MiddleUp();
                else await _mouseEmulator.MiddleClickAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PointerAction.X1Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X1Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X1Up();
                else await _mouseEmulator.X1ClickAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PointerAction.X2Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X2Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X2Up();
                else await _mouseEmulator.X2ClickAsync(cancellationToken).ConfigureAwait(false);
                break;
            case PointerAction.WheelUp:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelUp();
                break;
            case PointerAction.WheelDown:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelDown();
                break;
        }
    }

    private void QueueOutputDispatch(
        string buttonName,
        TriggerMoment trigger,
        DispatchedOutput output,
        string outputLabel,
        string sourceToken)
    {
        _inputDispatcher.Enqueue(buttonName, trigger, output, outputLabel, sourceToken);
    }

    private void EnqueueItemCycleTap(
        string sourceToken,
        TriggerMoment chordEnqueueTrigger,
        Key[] modifierKeys,
        string label,
        DispatchedOutput? customOutput,
        Key digitKey)
    {
        if (digitKey != Key.None)
        {
            _inputDispatcher.EnqueueChordTap(sourceToken, chordEnqueueTrigger, modifierKeys, digitKey, label, sourceToken);
            return;
        }

        if (customOutput is not { } o)
            throw new InvalidOperationException("Item cycle dispatch requires a digit or custom output.");

        if (o.KeyboardKey is { } k && k != Key.None && modifierKeys.Length > 0)
        {
            _inputDispatcher.EnqueueChordTap(sourceToken, TriggerMoment.Tap, modifierKeys, k, label, sourceToken);
            return;
        }

        QueueOutputDispatch(sourceToken, TriggerMoment.Tap, o, label, string.Empty);
    }

    private bool TryDispatchTemplateToggle(
        MappingEntry mapping,
        TriggerMoment trigger,
        string sourceToken,
        out string? errorStatus)
    {
        errorStatus = null;
        if (mapping.TemplateToggle is not { } tt)
            return false;

        if (trigger == TriggerMoment.Released)
            return true;

        if (!CanDispatchOutputMerged())
            return true;

        var profileId = tt.AlternateProfileId?.Trim() ?? string.Empty;
        if (profileId.Length == 0)
        {
            errorStatus = "Toggle profile: missing alternateProfileId.";
            return true;
        }

        var label = $"Toggle profile → {profileId}";
        _setMappedOutput($"{label} ({trigger})");
        _setMappingStatus($"Queued: {sourceToken} ({trigger}) -> {label}");
        _requestTemplateSwitchToProfileId?.Invoke(profileId);
        return true;
    }

    private RadialMenuDefinition? _activeRadialMenuDefinition;
    private MappingEntry? _radialMenuOpenMapping;
    private string _radialMenuSourceToken = string.Empty;
    private int _currentRadialSelectedIndex = -1;

    /// <summary>Neutral stick for analog mappings while radial uses that stick for selection; avoids look/move and releases held keys.</summary>
    private static Vector2 ThumbstickValueForAnalogWhileRadialOpen(
        RadialMenuDefinition? activeRadial,
        GamepadBindingType stick,
        Vector2 frameValue)
    {
        if (activeRadial is null)
            return frameValue;

        return RadialMenuJoystickToBindingType(activeRadial.Joystick) == stick
            ? Vector2.Zero
            : frameValue;
    }

    private static GamepadBindingType RadialMenuJoystickToBindingType(string? joystick)
    {
        var j = (joystick ?? string.Empty).Trim();
        if (j.Equals("LeftStick", StringComparison.OrdinalIgnoreCase))
            return GamepadBindingType.LeftThumbstick;
        return GamepadBindingType.RightThumbstick;
    }

    private void UpdateRadialMenuSelection(InputFrame frame)
    {
        if (_activeRadialMenuDefinition == null)
            return;

        var stick = _activeRadialMenuDefinition.Joystick == "LeftStick"
            ? frame.LeftThumbstick
            : frame.RightThumbstick;

        var minMag = Math.Clamp(_getRadialMenuStickEngagementThreshold(), 0.01f, 1f);
        var engaged = stick.Length() >= minMag;
        var confirmMode = _getRadialMenuConfirmMode();

        if (engaged)
        {
            _radialStickEverEngagedWhileOpen = true;

            var itemCount = _activeRadialMenuDefinition.Items.Count;
            if (itemCount == 0)
            {
                _radialPrevStickEngaged = true;
                return;
            }

            var angleRad = Math.Atan2(stick.X, stick.Y);
            var angleDeg = angleRad * (180.0 / Math.PI);
            if (angleDeg < 0) angleDeg += 360;

            var sectorSize = 360.0 / itemCount;
            var index = (int)((angleDeg + (sectorSize / 2)) / sectorSize) % itemCount;

            _radialLastSectorWhileEngaged = index;
            if (index != _currentRadialSelectedIndex)
            {
                _currentRadialSelectedIndex = index;
                _runOnUi(() => _radialMenuHud.UpdateSelection(index));
            }
        }
        else
        {
            if (confirmMode == RadialMenuConfirmMode.ReturnStickToCenter &&
                _radialPrevStickEngaged &&
                _radialStickEverEngagedWhileOpen &&
                _radialLastSectorWhileEngaged >= 0)
            {
                _currentRadialSelectedIndex = _radialLastSectorWhileEngaged;
                CloseRadialMenuSession(_radialMenuSourceToken, dispatchSelection: true, suppressChordUntilPhysicalRelease: true);
                _radialPrevStickEngaged = false;
                return;
            }

            if (_currentRadialSelectedIndex != -1)
            {
                _currentRadialSelectedIndex = -1;
                _runOnUi(() => _radialMenuHud.UpdateSelection(-1));
            }
        }

        _radialPrevStickEngaged = engaged;
    }

    private bool TryDispatchRadialMenu(
        MappingEntry mapping,
        TriggerMoment trigger,
        string sourceToken,
        out string? errorStatus)
    {
        errorStatus = null;
        if (mapping.RadialMenu is not { } rm)
            return false;

        if (trigger == TriggerMoment.Pressed)
        {
            var definition = _radialMenusPersist?.FirstOrDefault(d => d.Id == rm.RadialMenuId);
            if (definition == null)
            {
                errorStatus = "Radial menu: unknown id.";
                return true;
            }

            _radialMenuOpenMapping = mapping;
            _radialMenuSourceToken = sourceToken;
            _activeRadialMenuDefinition = definition;
            _currentRadialSelectedIndex = -1;

            var items = definition.Items.Select(item =>
            {
                var action = _keyboardActionsPersist?.FirstOrDefault(a => a.Id == item.ActionId);
                var desc = (action?.Description ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(desc))
                    desc = item.ActionId;
                var keyLabel = (action?.KeyboardKey ?? string.Empty).Trim();
                return new RadialMenuHudItem
                {
                    ActionId = item.ActionId,
                    DisplayName = desc,
                    KeyboardKeyLabel = keyLabel,
                    Icon = item.Icon
                };
            }).ToList();

            _radialPrevStickEngaged = false;
            _radialStickEverEngagedWhileOpen = false;
            _radialLastSectorWhileEngaged = -1;

            _runOnUi(() => _radialMenuHud.ShowMenu(definition.DisplayName, items));
            return true;
        }

        if (trigger == TriggerMoment.Released)
        {
            if (_activeRadialMenuDefinition is null ||
                !string.Equals(rm.RadialMenuId, _activeRadialMenuDefinition.Id, StringComparison.Ordinal))
                return false;

            var confirmOnRelease = _getRadialMenuConfirmMode() != RadialMenuConfirmMode.ReturnStickToCenter;
            CloseRadialMenuSession(sourceToken, dispatchSelection: confirmOnRelease);
            return true;
        }

        return false;
    }

    private void TryFinishRadialMenuOnChordRelease(GamepadButtons releasedButton)
    {
        if (_activeRadialMenuDefinition is null || _radialMenuOpenMapping is null)
            return;

        if (_radialMenuOpenMapping.From is not { } from || from.Type != GamepadBindingType.Button)
            return;

        if (!ChordResolver.TryParseButtonChord(
                from.Value,
                out var chordButtons,
                out _,
                out _,
                out _))
            return;

        if (chordButtons.Count == 0)
            return;

        if (!chordButtons.Contains(releasedButton))
            return;

        var confirmOnRelease = _getRadialMenuConfirmMode() != RadialMenuConfirmMode.ReturnStickToCenter;
        CloseRadialMenuSession(_radialMenuSourceToken, dispatchSelection: confirmOnRelease);
    }

    private void CloseRadialMenuSession(
        string sourceTokenForDispatch,
        bool dispatchSelection,
        bool suppressChordUntilPhysicalRelease = false)
    {
        if (_activeRadialMenuDefinition is null)
            return;

        var selectedIndex = _currentRadialSelectedIndex;
        var definition = _activeRadialMenuDefinition;

        _runOnUi(() => _radialMenuHud.HideMenu());

        if (dispatchSelection &&
            selectedIndex >= 0 &&
            selectedIndex < definition.Items.Count)
        {
            var item = definition.Items[selectedIndex];
            var action = _keyboardActionsPersist?.FirstOrDefault(a => a.Id == item.ActionId);
            var keyToken = action?.KeyboardKey ?? string.Empty;

            if (InputTokenResolver.TryResolveMappedOutput(keyToken, out var output, out var baseLabel))
            {
                var outputLabel = $"{baseLabel} (Radial)";
                _setMappedOutput(outputLabel);
                _setMappingStatus($"Radial Menu Selection: {item.ActionId} -> {outputLabel}");
                _inputDispatcher.Enqueue(sourceTokenForDispatch, TriggerMoment.Tap, output, outputLabel, keyToken);
            }
        }

        if (suppressChordUntilPhysicalRelease && dispatchSelection)
            BeginRadialChordButtonSuppression();

        ClearRadialSessionState();
    }

    private void BeginRadialChordButtonSuppression()
    {
        if (_radialMenuOpenMapping?.From is not { } from || from.Type != GamepadBindingType.Button)
            return;
        if (!ChordResolver.TryParseButtonChord(from.Value, out var chordButtons, out _, out _, out _))
            return;
        if (chordButtons.Count == 0)
            return;

        _radialChordSuppressionButtons = new HashSet<GamepadButtons>(chordButtons);
    }

    private bool SuppressRadialChordButtonMapping(ButtonEventContext context)
    {
        if (_radialChordSuppressionButtons is null)
            return false;
        if (!_radialChordSuppressionButtons.Contains(context.Button))
            return false;

        if (context.Trigger == TriggerMoment.Released)
        {
            _radialChordSuppressionButtons.Remove(context.Button);
            if (_radialChordSuppressionButtons.Count == 0)
                _radialChordSuppressionButtons = null;
        }

        return true;
    }

    private void ClearRadialSessionState()
    {
        _activeRadialMenuDefinition = null;
        _radialMenuOpenMapping = null;
        _radialMenuSourceToken = string.Empty;
        _currentRadialSelectedIndex = -1;
        _radialPrevStickEngaged = false;
        _radialStickEverEngagedWhileOpen = false;
        _radialLastSectorWhileEngaged = -1;
    }

    private void AbandonRadialMenuSessionForForceReset()
    {
        if (_activeRadialMenuDefinition is null)
            return;

        _radialChordSuppressionButtons = null;
        _runOnUi(() => _radialMenuHud.HideMenu());
        ClearRadialSessionState();
    }
}
