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
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Core.Processing;

namespace GamepadMapperGUI.Core;

public sealed class MappingEngine : IMappingEngine, IKeyboardActionExecutor
{
    private readonly IProfileService _profileService;
    private readonly IKeyboardEmulator _keyboardEmulator;
    private readonly IMouseEmulator _mouseEmulator;
    private readonly Func<bool> _canDispatchOutputLive;
    private bool _processingInputFrame;
    private bool _frameDispatchAllowed;
    private readonly Action<string> _setMappedOutput;
    private readonly Action<string> _setMappingStatus;
    private readonly Action<string, TriggerMoment, Key[], string, DispatchedOutput?, Key> _enqueueItemCycleTap;
    private readonly Action<string, TriggerMoment, Key[], Key, string, string> _enqueueChordTap;
    private readonly OutputStateTracker _outputStateTracker = new();
    private readonly AnalogProcessor _analogProcessor = new();
    private readonly IInputDispatcher _inputDispatcher;
    private readonly Action<ComboHudContent?>? _setComboHud;
    private readonly object _inputFrameSync = new();
    private readonly HoldSessionManager _holdSessionManager;
    private readonly InputFramePipeline _inputFramePipeline;
    private readonly ButtonEventPipeline _buttonEventPipeline;
    private readonly ItemCycleProcessor _itemCycleProcessor = new();
    private readonly ActiveActionTracker _activeActionTracker = new();

    private readonly int _comboHudDelayMs;
    private readonly int _leadKeyReleaseSuppressMs;

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

    private readonly AnalogMappingProcessor _analogMappingProcessor;
    private readonly ComboHudManager? _comboHudManager;
    private readonly ButtonMappingProcessor _buttonMappingProcessor;
    private readonly IRadialMenuController _radialMenuController;
    private readonly ITimeProvider _timeProvider;
    private readonly Action<Action> _runOnUi;
    private readonly IRadialMenuHud _radialMenuHud;
    private readonly Func<float> _getRadialMenuStickEngagementThreshold;
    private readonly Func<RadialMenuConfirmMode> _getRadialMenuConfirmMode;

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
        IProfileService? profileService = null,
        Action<string?>? setComboHudGateHint = null,
        Func<string>? comboHudGateMessageFactory = null,
        Func<bool>? isComboHudPresentationSuppressed = null,
        IRadialMenuHud? radialMenuHud = null,
        Func<float>? getRadialMenuStickEngagementThreshold = null,
        Func<RadialMenuConfirmMode>? getRadialMenuConfirmMode = null,
        ITimeProvider? timeProvider = null,
        IInputDispatcher? inputDispatcher = null) // Added optional parameter
    {
        _timeProvider = timeProvider ?? new RealTimeProvider();
        _runOnUi = runOnUi;
        _radialMenuHud = radialMenuHud ?? NullRadialMenuHud.Instance;
        _getRadialMenuStickEngagementThreshold =
            getRadialMenuStickEngagementThreshold ?? (() => 0.35f);
        _getRadialMenuConfirmMode =
            getRadialMenuConfirmMode ?? (() => RadialMenuConfirmMode.ReturnStickToCenter);
        _comboHudDelayMs = modifierGraceMs;
        _leadKeyReleaseSuppressMs = leadKeyReleaseSuppressMs;
        _requestTemplateSwitchToProfileId = requestTemplateSwitchToProfileId;
        _keyboardEmulator = keyboardEmulator;
        _mouseEmulator = mouseEmulator;
        _profileService = profileService ?? new ProfileService();
        _canDispatchOutputLive = canDispatchOutputLive;
        _setMappedOutput = setMappedOutput;
        _setMappingStatus = setMappingStatus;
        _setComboHud = setComboHud;
        
        _inputDispatcher = inputDispatcher ?? new InputDispatcher(
            DispatchMappedOutputAsync,
            (modifiers, mainKey, ct) => _keyboardEmulator.TapKeyChordAsync(modifiers, mainKey, cancellationToken: ct),
            runOnUi,
            setMappedOutput,
            setMappingStatus);

        _enqueueItemCycleTap = EnqueueItemCycleTap;
        _enqueueChordTap = (source, trigger, mods, key, label, token) => _inputDispatcher.EnqueueChordTap(source, trigger, mods, key, label, token);

        _radialMenuController = new RadialMenuController(
            _radialMenuHud,
            _runOnUi,
            _setMappedOutput,
            _setMappingStatus,
            QueueOutputDispatch,
            () => _getRadialMenuConfirmMode(),
            action => _activeActionTracker.Register(action),
            id => _activeActionTracker.Unregister(id),
            _requestTemplateSwitchToProfileId,
            null); // Catalog will be set when profile is loaded

        _radialMenuController.SetActionExecutor(this);

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
        _enqueueItemCycleTap,
        _enqueueChordTap,
        TryDispatchAction,
        mappings => ResolveComboLeads(mappings),
        _leadKeyReleaseSuppressMs,
        _deferredSoloLeadButtons,
        _deferredSoloLeadButtonsLock,
        activeActionTracker: _activeActionTracker);

        var inputStateSyncMiddleware = new InputStateSyncMiddleware(
            (activeButtons, leftTrigger, rightTrigger) =>
            {
                _latestActiveButtons = activeButtons;
                _latestLeftTrigger = leftTrigger;
                _latestRightTrigger = rightTrigger;
                _holdSessionManager.UpdateLatestInputState(activeButtons, leftTrigger, rightTrigger);
            });

        var radialMiddleware = new RadialMenuMiddleware(
            _radialMenuController,
            _getRadialMenuStickEngagementThreshold,
            _getRadialMenuConfirmMode);

        var buttonTransitionMiddleware = new ButtonTransitionMiddleware(
            handleButtonEvent: HandleButtonMappingsInternal,
            getMappingsSnapshot: () => _lastButtonMappingsSnapshot,
            onButtonReleased: _activeActionTracker.ProcessButtonReleased);

        var analogTransitionMiddleware = new AnalogTransitionMiddleware(
            _analogMappingProcessor,
            QueueOutputDispatch,
            ProcessNativeTriggerExecutableActions,
            () => _lastButtonMappingsSnapshot);

        _inputFramePipeline = new InputFramePipeline(
            middlewares: [new InputFrameTransitionMiddleware(), inputStateSyncMiddleware, radialMiddleware, buttonTransitionMiddleware, analogTransitionMiddleware],
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
                    setMappingStatus: _setMappingStatus,
                    cancelDualActionSuperseded: _holdSessionManager.CancelDualActionSupersededByMoreSpecificChord)
            ],
            terminal: _buttonMappingProcessor.ProcessButtonEventTerminal);
    }

    /// <inheritdoc />
    public void SetComboLeadButtonsFromTemplate(IReadOnlyList<string>? comboLeadButtonNames) =>
        _explicitComboLeadsFromTemplate = ComboLeadSemantics.ParseDeclaredNames(comboLeadButtonNames);

    public void SetRadialMenuDefinitions(List<RadialMenuDefinition>? radialMenus, List<KeyboardActionDefinition>? keyboardActions, IKeyboardActionCatalog? catalog = null)
    {
        _radialMenuController.SetDefinitions(radialMenus, keyboardActions, catalog);
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

                // Resolve executable actions for the current snapshot if not already done
                foreach (var mapping in _lastButtonMappingsSnapshot)
                {
                    mapping.ExecutableAction ??= ResolveExecutableAction(mapping);
                }

                var context = new InputFrameContext
                {
                    Frame = frame
                };

                _inputFramePipeline.Invoke(context);

                return new InputFrameProcessingResult(
                    context.PressedButtons,
                    context.ReleasedButtons);
            }
            finally
            {
                _processingInputFrame = false;
            }
        }
    }

    private void ProcessInputFrameTerminal(InputFrameContext context)
    {
        TrySyncComboHud(context);
    }

    /// <summary>
    /// Radial menu / item cycle / template toggle on <see cref="GamepadBindingType.LeftTrigger"/> or <see cref="GamepadBindingType.RightTrigger"/> (same shape as JSON templates).
    /// </summary>
    private void ProcessNativeTriggerExecutableActions(float triggerValue, GamepadBindingType side)
    {
        if (!CanDispatchOutputMerged())
            return;

        foreach (var mapping in _lastButtonMappingsSnapshot)
        {
            if (mapping?.From is null || mapping.From.Type != side)
                continue;
            if (mapping.ActionType == MappingActionType.Keyboard)
                continue;

            mapping.ExecutableAction ??= ResolveExecutableAction(mapping);

            var stateId = BuildNativeTriggerActionStateId(mapping, side);
            var transition = _analogProcessor.EvaluateTriggerEdge(stateId, mapping, triggerValue);
            if (!transition.HasChanged)
                continue;

            var moment = transition.IsActive ? TriggerMoment.Pressed : TriggerMoment.Released;

            if (!ShouldDispatchNativeTriggerEdgeMoment(mapping, moment))
                continue;

            var sourceToken = mapping.From.Value ?? side.ToString();
            TryDispatchAction(mapping, moment, sourceToken, out var err);
            if (!string.IsNullOrEmpty(err))
                _setMappingStatus(err);
        }
    }

    private static bool ShouldDispatchNativeTriggerEdgeMoment(MappingEntry mapping, TriggerMoment moment)
    {
        if (mapping.ActionType == MappingActionType.RadialMenu)
            return true;

        return mapping.Trigger == moment;
    }

    private static Processing.AnalogStateId BuildNativeTriggerActionStateId(MappingEntry mapping, GamepadBindingType side)
    {
        if (mapping.RadialMenu is { } rm)
            return new Processing.AnalogStateId(side, mapping.ActionType, rm.RadialMenuId);
        if (mapping.TemplateToggle is { } tt)
            return new Processing.AnalogStateId(side, mapping.ActionType, tt.AlternateProfileId);
        
        return new Processing.AnalogStateId(side, mapping.ActionType, mapping.KeyboardKey, mapping.Description);
    }

    private IExecutableAction? ResolveExecutableAction(MappingEntry mapping)
    {
        return mapping.ActionType switch
        {
            MappingActionType.RadialMenu => new Actions.RadialMenuAction(mapping, _radialMenuController),
            MappingActionType.ItemCycle => new Actions.ItemCycleAction(mapping, _itemCycleProcessor, () => CanDispatchOutputMerged(), _setMappedOutput, _setMappingStatus, _enqueueItemCycleTap),
            MappingActionType.TemplateToggle => new Actions.TemplateToggleAction(mapping, () => CanDispatchOutputMerged(), _setMappedOutput, _setMappingStatus, _requestTemplateSwitchToProfileId),
            MappingActionType.Keyboard when mapping.From.Type != GamepadBindingType.Button =>
                new Actions.KeyboardAction(mapping.KeyboardKey ?? string.Empty, TryDispatchLegacyKeyboard),
            MappingActionType.Keyboard => new Actions.KeyboardAction(mapping.KeyboardKey ?? string.Empty, TryDispatchLegacyKeyboard),
            _ => null,
        };
    }

    /// <summary>
    /// Keyboard dispatch for non-button sources (triggers, thumbsticks) where <see cref="ButtonMappingProcessor"/> does not run.
    /// Button mappings use <c>null</c> <see cref="MappingEntry.ExecutableAction"/> and the processor's unified path with <see cref="OutputStateTracker"/>.
    /// </summary>
    private bool TryDispatchLegacyKeyboard(string keyboardKey, TriggerMoment trigger, out string? errorStatus)
    {
        errorStatus = null;
        if (!InputTokenResolver.TryResolveMappedOutput(keyboardKey, out var output, out var baseLabel))
            return false;

        var outputLabel = $"{baseLabel} ({trigger})";
        _setMappedOutput(outputLabel);
        _setMappingStatus($"{keyboardKey} ({trigger}) -> {outputLabel}");
        QueueOutputDispatch("LegacySource", trigger, output, outputLabel, keyboardKey);
        return true;
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
                _activeActionTracker.ForceReleaseAll();
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
        var command = TranslateToCommand(output, trigger);
        if (command.Type == OutputCommandType.None) return;

        if (command.Type is OutputCommandType.KeyPress or OutputCommandType.KeyRelease or OutputCommandType.KeyTap or OutputCommandType.Text)
        {
            await _keyboardEmulator.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _mouseEmulator.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private static OutputCommand TranslateToCommand(DispatchedOutput output, TriggerMoment trigger)
    {
        if (output.KeyboardKey is { } key && key != Key.None)
        {
            var type = trigger switch
            {
                TriggerMoment.Pressed => OutputCommandType.KeyPress,
                TriggerMoment.Released => OutputCommandType.KeyRelease,
                _ => OutputCommandType.KeyTap
            };
            return new OutputCommand(type, Key: key);
        }

        if (output.PointerAction is { } action && action != PointerAction.None)
        {
            var type = action switch
            {
                PointerAction.WheelUp or PointerAction.WheelDown => OutputCommandType.PointerWheel,
                _ => trigger switch
                {
                    TriggerMoment.Pressed => OutputCommandType.PointerDown,
                    TriggerMoment.Released => OutputCommandType.PointerUp,
                    _ => OutputCommandType.PointerClick
                }
            };
            return new OutputCommand(type, PointerAction: action);
        }

        return default;
    }

    private void ForceReleaseHeldOutputsForButton(GamepadButtons button, IReadOnlySet<DispatchedOutput>? outputsHandledByReleasedMappings = null)
    {
        _outputStateTracker.ForceReleaseHeldOutputsForButton(button, ForceReleaseOutput, outputsHandledByReleasedMappings);
    }

    private void ForceReleaseOutput(DispatchedOutput output)
    {
        // Use the dispatcher to ensure consistent ordering even for forced releases
        QueueOutputDispatch("ForceRelease", TriggerMoment.Released, output, "Forced release", "forced-release");
    }

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

    private bool TryDispatchAction(
        MappingEntry mapping,
        TriggerMoment trigger,
        string sourceToken,
        out string? errorStatus)
    {
        if (mapping.ExecutableAction is { } action)
        {
            return action.Execute(trigger, sourceToken, out errorStatus);
        }

        errorStatus = null;
        return false;
    }

    private void SendMouseLookDelta(GamepadBindingType thumbstickSource, float deltaX, float deltaY, float stickMagnitude)
    {
        var delta = _analogProcessor.AccumulateMouseLookDelta(thumbstickSource, deltaX, deltaY);
        // Mouse movement is currently direct because it's high-frequency (every frame) 
        // and doesn't have a "hold duration" that would block the input loop.
        _mouseEmulator.MoveBy(delta.PixelDx, delta.PixelDy, stickMagnitude);
    }

    public bool Execute(KeyboardActionDefinition action, string sourceToken, out string? errorStatus)
    {
        errorStatus = null;
        if (!CanDispatchOutputMerged())
            return false;

        if (action.RadialMenu is { } rm)
        {
            // Note: Opening a radial menu from another radial menu is technically possible 
            // but might need careful state management in the controller.
            var mapping = new MappingEntry { RadialMenu = rm };
            return _radialMenuController.TryOpen(mapping, sourceToken, out errorStatus);
        }

        if (action.TemplateToggle is { } tt)
        {
            var targetId = tt.AlternateProfileId;
            _setMappingStatus($"Action: {action.Id} -> Toggle profile {targetId}");
            _setMappedOutput($"Toggle profile → {targetId}");
            _requestTemplateSwitchToProfileId?.Invoke(targetId);
            return true;
        }

        if (action.ItemCycle is { } ic)
        {
            var mapping = new MappingEntry { ItemCycle = ic };
            var executable = new Actions.ItemCycleAction(mapping, _itemCycleProcessor, () => CanDispatchOutputMerged(), _setMappedOutput, _setMappingStatus, _enqueueItemCycleTap);
            return executable.Execute(TriggerMoment.Tap, sourceToken, out errorStatus);
        }

        var keyToken = action.KeyboardKey ?? string.Empty;
        if (InputTokenResolver.TryResolveMappedOutput(keyToken, out var output, out var baseLabel))
        {
            var outputLabel = baseLabel;
            _setMappedOutput(outputLabel);
            _setMappingStatus($"Action: {action.Id} -> {outputLabel}");
            QueueOutputDispatch(sourceToken, TriggerMoment.Tap, output, outputLabel, keyToken);
            return true;
        }

        return false;
    }
}


