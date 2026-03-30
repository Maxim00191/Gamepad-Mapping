using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using Vortice.XInput;

namespace Gamepad_Mapping.ViewModels;

public class TemplateOption
{
    public string ProfileId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly record struct DispatchedOutput(Key? KeyboardKey, PointerAction? PointerAction);
    private readonly record struct AnalogOutputState(bool IsActive);
    private readonly record struct AnalogSourceDefinition(bool IsDirectional, bool IsSignedAxis, bool IsVerticalAxis, int DirectionSign);
    private readonly record struct QueuedOutputWork(
        string ButtonName,
        TriggerMoment Trigger,
        DispatchedOutput Output,
        string OutputLabel,
        string SourceToken);

    private enum KeyCaptureTarget
    {
        None,
        SelectedMapping,
        NewBinding
    }

    private enum PointerAction
    {
        LeftClick,
        RightClick,
        MiddleClick,
        X1Click,
        X2Click,
        WheelUp,
        WheelDown
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> KeyAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Spacebar"] = nameof(Key.Space),
            ["Return"] = nameof(Key.Enter),
            ["Esc"] = nameof(Key.Escape),
            ["LeftControl"] = nameof(Key.LeftCtrl),
            ["RightControl"] = nameof(Key.RightCtrl),
            ["Control"] = nameof(Key.LeftCtrl),
            ["Ctrl"] = nameof(Key.LeftCtrl),
            ["Alt"] = nameof(Key.LeftAlt)
        };

    private readonly Dispatcher _dispatcher;
    private readonly ProfileService _profileService;
    private readonly GamepadReader _gamepadReader;
    private readonly KeyboardEmulator _keyboardEmulator;
    private readonly MouseEmulator _mouseEmulator;
    private readonly ProcessTargetService _processTargetService;
    private readonly bool _isCurrentProcessElevated;
    private int? _lastElevationPromptedProcessId;

    private readonly TriggerMoment _buttonPressedTrigger = TriggerMoment.Pressed;
    private readonly TriggerMoment _buttonReleasedTrigger = TriggerMoment.Released;
    private readonly TriggerMoment _buttonTapTrigger = TriggerMoment.Tap;
    private readonly Dictionary<GamepadButtons, HashSet<DispatchedOutput>> _activeHeldOutputsByButton = new();
    private readonly object _outputQueueLock = new();
    private readonly Queue<QueuedOutputWork> _outputQueue = new();
    private readonly SemaphoreSlim _outputQueueSignal = new(0);
    private readonly CancellationTokenSource _outputQueueCts = new();
    private readonly Task _outputQueueWorkerTask;
    private readonly Dictionary<string, AnalogOutputState> _analogOutputStates = new();
    private KeyCaptureTarget _keyCaptureTarget = KeyCaptureTarget.None;
    private float _mouseLookResidualX;
    private float _mouseLookResidualY;

    private const float DefaultAnalogThreshold = 0.35f;
    private const float DefaultMouseLookSensitivity = 18f;

    public MainViewModel()
    {
        // Ensure property updates land on the UI thread (gamepad events originate on a background thread).
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _profileService = new ProfileService();
        _gamepadReader = new GamepadReader();
        _keyboardEmulator = new KeyboardEmulator();
        _mouseEmulator = new MouseEmulator();
        _processTargetService = new ProcessTargetService();
        _isCurrentProcessElevated = _processTargetService.IsCurrentProcessElevated();

        AvailableTemplates = new ObservableCollection<TemplateOption>();
        RecentProcesses = new ObservableCollection<ProcessInfo>();
        Mappings = new ObservableCollection<MappingEntry>();
        AvailableGamepadButtons = new ObservableCollection<string>(
            Enum.GetNames<GamepadButtons>()
                .Where(n => !string.Equals(n, nameof(GamepadButtons.None), StringComparison.OrdinalIgnoreCase)));
        AvailableTriggerModes = new ObservableCollection<TriggerMoment>(
            Enum.GetValues<TriggerMoment>());
        NewBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
        NewBindingTrigger = TriggerMoment.Tap;
        Mappings.CollectionChanged += (_, _) => MappingCount = Mappings.Count;

        _gamepadReader.OnButtonPressed += buttons =>
            DispatchToUi(() =>
            {
                LastButtonPressed = buttons.ToString();
                ApplyButtonMappings(buttons, _buttonPressedTrigger);
                ApplyButtonMappings(buttons, _buttonTapTrigger);
            });
        _gamepadReader.OnButtonReleased += buttons =>
            DispatchToUi(() =>
            {
                LastButtonReleased = buttons.ToString();
                ApplyButtonMappings(buttons, _buttonReleasedTrigger);
            });
        _gamepadReader.OnLeftThumbstickChanged += v =>
            DispatchToUi(() =>
            {
                LeftThumbX = v.X;
                LeftThumbY = v.Y;
                ApplyThumbstickMappings(GamepadBindingType.LeftThumbstick, v);
            });
        _gamepadReader.OnRightThumbstickChanged += v =>
            DispatchToUi(() =>
            {
                RightThumbX = v.X;
                RightThumbY = v.Y;
                ApplyThumbstickMappings(GamepadBindingType.RightThumbstick, v);
            });
        _gamepadReader.OnLeftTriggerChanged += v =>
            DispatchToUi(() => LeftTrigger = v);
        _gamepadReader.OnRightTriggerChanged += v =>
            DispatchToUi(() => RightTrigger = v);

        LoadTemplates();
        LoadSelectedTemplate();

        ProfileTemplatePanel = new ProfileTemplatePanelViewModel(this);
        NewBindingPanel = new NewBindingPanelViewModel(this);
        MappingEditorPanel = new MappingEditorViewModel(this);
        GamepadMonitorPanel = new GamepadMonitorViewModel(this);
        ProcessTargetPanel = new ProcessTargetPanelViewModel(this);
        _outputQueueWorkerTask = Task.Run(ProcessOutputQueueAsync);

        RefreshProcesses();
        StartGamepad();
    }

    private void ApplyButtonMappings(GamepadButtons buttons, TriggerMoment trigger)
    {
        if (trigger == TriggerMoment.Released)
        {
            // Always release previously held outputs even if focus changed (prevents stuck buttons).
            ForceReleaseHeldOutputs(buttons);
        }
        else if (!ShouldSendKeys())
        {
            LastMappingStatus = $"Suppressed ({buttons}, {trigger}) - target is not foreground";
            return;
        }

        var buttonName = buttons.ToString();
        var snapshot = Mappings.ToList();
        var matched = false;

        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null) continue;
            if (mapping.From.Type != GamepadBindingType.Button) continue;
            if (!string.Equals(mapping.From.Value, buttonName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (mapping.Trigger != trigger) continue;
            matched = true;

            try
            {
                if (!TryResolveMappedOutput(mapping.KeyboardKey, out var output, out var baseLabel))
                    continue;

                TrackOutputHoldState(buttons, output, trigger);
                var outputLabel = $"{baseLabel} ({trigger})";
                LastMappedOutput = outputLabel;
                LastMappingStatus = $"Queued: {buttonName} ({trigger}) -> {outputLabel}";
                QueueOutputDispatch(buttonName, trigger, output, outputLabel, mapping.KeyboardKey ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send key mapping. key={mapping.KeyboardKey}, ex={ex.Message}");
                LastMappingStatus = $"Error sending '{mapping.KeyboardKey}': {ex.Message}";
            }
        }

        if (!matched)
            LastMappingStatus = $"No mapping for {buttonName} ({trigger})";
    }

    private bool TryResolveMappedOutput(string? outputToken, out DispatchedOutput output, out string outputLabel)
    {
        output = default;
        outputLabel = string.Empty;
        var normalized = NormalizeKeyboardKeyToken(outputToken ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (TryParsePointerAction(normalized, out var pointerAction))
        {
            output = new DispatchedOutput(null, pointerAction);
            outputLabel = DescribePointerAction(pointerAction);
            return true;
        }

        var key = ParseKey(normalized);
        if (key == Key.None)
            return false;

        output = new DispatchedOutput(key, null);
        outputLabel = $"Keyboard {key}";
        return true;
    }

    private void DispatchMappedOutput(DispatchedOutput output, TriggerMoment trigger)
    {
        if (output.KeyboardKey is Key key && key != Key.None)
        {
            if (trigger == TriggerMoment.Pressed)
                _keyboardEmulator.KeyDown(key);
            else if (trigger == TriggerMoment.Released)
                _keyboardEmulator.KeyUp(key);
            else
                _keyboardEmulator.TapKey(key);
            return;
        }

        if (output.PointerAction is PointerAction pointerAction)
            SendPointerAction(pointerAction, trigger);
    }

    private void TrackOutputHoldState(GamepadButtons button, DispatchedOutput output, TriggerMoment trigger)
    {
        if (trigger == TriggerMoment.Pressed && IsHoldableOutput(output))
        {
            if (!_activeHeldOutputsByButton.TryGetValue(button, out var heldOutputs))
            {
                heldOutputs = [];
                _activeHeldOutputsByButton[button] = heldOutputs;
            }

            heldOutputs.Add(output);
            return;
        }

        if (trigger != TriggerMoment.Released)
            return;

        if (_activeHeldOutputsByButton.TryGetValue(button, out var existing))
        {
            existing.Remove(output);
            if (existing.Count == 0)
                _activeHeldOutputsByButton.Remove(button);
        }
    }

    private void ForceReleaseHeldOutputs(GamepadButtons button)
    {
        if (!_activeHeldOutputsByButton.TryGetValue(button, out var heldOutputs) || heldOutputs.Count == 0)
            return;

        foreach (var heldOutput in heldOutputs.ToList())
        {
            ForceReleaseOutput(heldOutput);
        }

        _activeHeldOutputsByButton.Remove(button);
    }

    private void ForceReleaseAllHeldOutputs()
    {
        foreach (var heldOutputs in _activeHeldOutputsByButton.Values)
        {
            foreach (var heldOutput in heldOutputs.ToList())
            {
                ForceReleaseOutput(heldOutput);
            }
        }

        _activeHeldOutputsByButton.Clear();
    }

    private void ForceReleaseOutput(DispatchedOutput output)
    {
        QueueOutputDispatch("ForceRelease", TriggerMoment.Released, output, "Forced release", "forced-release");
    }

    private static bool IsHoldableOutput(DispatchedOutput output)
    {
        if (output.KeyboardKey is Key key && key != Key.None)
            return true;

        return output.PointerAction is PointerAction.LeftClick
            or PointerAction.RightClick
            or PointerAction.MiddleClick
            or PointerAction.X1Click
            or PointerAction.X2Click;
    }

    private static string DescribePointerAction(PointerAction action)
    {
        var label = action switch
        {
            PointerAction.LeftClick => "Mouse Left",
            PointerAction.RightClick => "Mouse Right",
            PointerAction.MiddleClick => "Mouse Middle",
            PointerAction.X1Click => "Mouse X1",
            PointerAction.X2Click => "Mouse X2",
            PointerAction.WheelUp => "Mouse Wheel Up",
            PointerAction.WheelDown => "Mouse Wheel Down",
            _ => "Mouse"
        };

        return label;
    }

    private void ApplyThumbstickMappings(GamepadBindingType sourceType, Vector2 stickValue)
    {
        var snapshot = Mappings.ToList();
        var mouseDeltaX = 0f;
        var mouseDeltaY = 0f;

        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null || mapping.From.Type != sourceType)
                continue;

            if (!TryParseAnalogSource(mapping.From.Value, out var source))
                continue;

            var outputToken = NormalizeKeyboardKeyToken(mapping.KeyboardKey ?? string.Empty);
            if (string.IsNullOrWhiteSpace(outputToken))
                continue;

            if (TryResolveMouseLookOutput(outputToken, out var isVerticalLook))
            {
                var axisValue = ResolveStickAxisValue(stickValue, source);
                if (MathF.Abs(axisValue) < 0.01f)
                    continue;

                var delta = axisValue * DefaultMouseLookSensitivity;
                if (isVerticalLook)
                    mouseDeltaY += -delta; // Windows mouse Y is inverted versus gamepad up.
                else
                    mouseDeltaX += delta;
                continue;
            }

            var key = ParseKey(outputToken);
            if (key == Key.None)
                continue;

            HandleAnalogKeyboardOutput(mapping, source, stickValue, key);
        }

        if (MathF.Abs(mouseDeltaX) > 0f || MathF.Abs(mouseDeltaY) > 0f)
            SendMouseLookDelta(mouseDeltaX, mouseDeltaY);
    }

    private void HandleAnalogKeyboardOutput(MappingEntry mapping, AnalogSourceDefinition source, Vector2 stickValue, Key key)
    {
        var threshold = mapping.AnalogThreshold is > 0 and <= 1 ? mapping.AnalogThreshold.Value : DefaultAnalogThreshold;
        var axisValue = ResolveStickAxisValue(stickValue, source);
        var isActive = axisValue >= threshold;
        var stateKey = BuildAnalogStateKey(mapping, key);
        _analogOutputStates.TryGetValue(stateKey, out var currentState);
        if (currentState.IsActive == isActive)
            return;

        _analogOutputStates[stateKey] = new AnalogOutputState(isActive);

        if (isActive)
        {
            if (mapping.Trigger == TriggerMoment.Tap)
                _keyboardEmulator.TapKey(key);
            else if (mapping.Trigger != TriggerMoment.Released)
                _keyboardEmulator.KeyDown(key);
        }
        else
        {
            if (mapping.Trigger == TriggerMoment.Released)
                _keyboardEmulator.TapKey(key);
            else if (mapping.Trigger != TriggerMoment.Tap)
                _keyboardEmulator.KeyUp(key);
        }
    }

    private void SendMouseLookDelta(float deltaX, float deltaY)
    {
        _mouseLookResidualX += deltaX;
        _mouseLookResidualY += deltaY;
        var pixelDx = (int)MathF.Truncate(_mouseLookResidualX);
        var pixelDy = (int)MathF.Truncate(_mouseLookResidualY);
        _mouseLookResidualX -= pixelDx;
        _mouseLookResidualY -= pixelDy;

        if (pixelDx != 0 || pixelDy != 0)
            _mouseEmulator.MoveBy(pixelDx, pixelDy);
    }

    private void ForceReleaseAnalogOutputs()
    {
        foreach (var kvp in _analogOutputStates.ToList())
        {
            if (!kvp.Value.IsActive)
                continue;

            var parts = kvp.Key.Split('|');
            if (parts.Length < 4)
                continue;

            var triggerToken = parts[3];
            if (string.Equals(triggerToken, TriggerMoment.Tap.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;

            var keyToken = parts[2];
            var key = ParseKey(keyToken);
            if (key != Key.None)
                _keyboardEmulator.KeyUp(key);
        }

        _analogOutputStates.Clear();
        _mouseLookResidualX = 0f;
        _mouseLookResidualY = 0f;
    }

    private static string BuildAnalogStateKey(MappingEntry mapping, Key key)
    {
        var sourceType = mapping.From.Type.ToString();
        var sourceValue = mapping.From.Value ?? string.Empty;
        var trigger = mapping.Trigger.ToString();
        return $"{sourceType}|{sourceValue}|{key}|{trigger}";
    }

    private static float ResolveStickAxisValue(Vector2 value, AnalogSourceDefinition source)
    {
        var axisRaw = source.IsVerticalAxis ? value.Y : value.X;
        if (source.IsSignedAxis)
            return axisRaw;

        if (!source.IsDirectional)
            return 0f;

        return source.DirectionSign >= 0
            ? MathF.Max(0f, axisRaw)
            : MathF.Max(0f, -axisRaw);
    }

    private static bool TryResolveMouseLookOutput(string token, out bool isVerticalLook)
    {
        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        if (normalized is "MOUSEX" or "MOUSELOOKX" or "LOOKX" or "VIEWX")
        {
            isVerticalLook = false;
            return true;
        }

        if (normalized is "MOUSEY" or "MOUSELOOKY" or "LOOKY" or "VIEWY")
        {
            isVerticalLook = true;
            return true;
        }

        isVerticalLook = false;
        return false;
    }

    private static bool TryParseAnalogSource(string token, out AnalogSourceDefinition source)
    {
        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        source = normalized switch
        {
            "RIGHT" or "POSX" or "XPOS" or "XP" or "XPLUS" => new AnalogSourceDefinition(true, false, false, +1),
            "LEFT" or "NEGX" or "XNEG" or "XM" or "XMINUS" => new AnalogSourceDefinition(true, false, false, -1),
            "UP" or "FORWARD" or "POSY" or "YPOS" or "YP" or "YPLUS" => new AnalogSourceDefinition(true, false, true, +1),
            "DOWN" or "BACK" or "BACKWARD" or "NEGY" or "YNEG" or "YM" or "YMINUS" => new AnalogSourceDefinition(true, false, true, -1),
            "X" or "HORIZONTAL" => new AnalogSourceDefinition(false, true, false, +1),
            "Y" or "VERTICAL" => new AnalogSourceDefinition(false, true, true, +1),
            _ => default
        };

        return normalized is
            "RIGHT" or "POSX" or "XPOS" or "XP" or "XPLUS" or
            "LEFT" or "NEGX" or "XNEG" or "XM" or "XMINUS" or
            "UP" or "FORWARD" or "POSY" or "YPOS" or "YP" or "YPLUS" or
            "DOWN" or "BACK" or "BACKWARD" or "NEGY" or "YNEG" or "YM" or "YMINUS" or
            "X" or "HORIZONTAL" or
            "Y" or "VERTICAL";
    }

    private void SendPointerAction(PointerAction action, TriggerMoment trigger)
    {
        switch (action)
        {
            case PointerAction.LeftClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.LeftDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.LeftUp();
                else _mouseEmulator.LeftClick();
                break;
            case PointerAction.RightClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.RightDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.RightUp();
                else _mouseEmulator.RightClick();
                break;
            case PointerAction.MiddleClick:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.MiddleDown();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.MiddleUp();
                else _mouseEmulator.MiddleClick();
                break;
            case PointerAction.X1Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X1Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X1Up();
                else _mouseEmulator.X1Click();
                break;
            case PointerAction.X2Click:
                if (trigger == TriggerMoment.Pressed) _mouseEmulator.X2Down();
                else if (trigger == TriggerMoment.Released) _mouseEmulator.X2Up();
                else _mouseEmulator.X2Click();
                break;
            case PointerAction.WheelUp:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelUp();
                break;
            case PointerAction.WheelDown:
                if (trigger != TriggerMoment.Released) _mouseEmulator.WheelDown();
                break;
        }
    }

    private static bool TryParsePointerAction(string token, out PointerAction action)
    {
        var normalized = token.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        action = normalized switch
        {
            "MOUSELEFT" or "LEFTCLICK" or "LCLICK" or "LBUTTON" => PointerAction.LeftClick,
            "MOUSERIGHT" or "RIGHTCLICK" or "RCLICK" or "RBUTTON" => PointerAction.RightClick,
            "MOUSEMIDDLE" or "MIDDLECLICK" or "MCLICK" or "MBUTTON" => PointerAction.MiddleClick,
            "MOUSEX1" or "XBUTTON1" or "XBUTTONONE" => PointerAction.X1Click,
            "MOUSEX2" or "XBUTTON2" or "XBUTTONTWO" => PointerAction.X2Click,
            "WHEELUP" or "MOUSEWHEELUP" or "SCROLLUP" => PointerAction.WheelUp,
            "WHEELDOWN" or "MOUSEWHEELDOWN" or "SCROLLDOWN" => PointerAction.WheelDown,
            _ => default
        };

        return normalized is
            "MOUSELEFT" or "LEFTCLICK" or "LCLICK" or "LBUTTON" or
            "MOUSERIGHT" or "RIGHTCLICK" or "RCLICK" or "RBUTTON" or
            "MOUSEMIDDLE" or "MIDDLECLICK" or "MCLICK" or "MBUTTON" or
            "MOUSEX1" or "XBUTTON1" or "XBUTTONONE" or
            "MOUSEX2" or "XBUTTON2" or "XBUTTONTWO" or
            "WHEELUP" or "MOUSEWHEELUP" or "SCROLLUP" or
            "WHEELDOWN" or "MOUSEWHEELDOWN" or "SCROLLDOWN";
    }

    private void QueueOutputDispatch(
        string buttonName,
        TriggerMoment trigger,
        DispatchedOutput output,
        string outputLabel,
        string sourceToken)
    {
        lock (_outputQueueLock)
        {
            _outputQueue.Enqueue(new QueuedOutputWork(buttonName, trigger, output, outputLabel, sourceToken));
        }

        _outputQueueSignal.Release();
    }

    private async Task ProcessOutputQueueAsync()
    {
        while (!_outputQueueCts.IsCancellationRequested)
        {
            try
            {
                await _outputQueueSignal.WaitAsync(_outputQueueCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            QueuedOutputWork workItem;
            lock (_outputQueueLock)
            {
                if (_outputQueue.Count == 0)
                    continue;

                workItem = _outputQueue.Dequeue();
            }

            try
            {
                DispatchMappedOutput(workItem.Output, workItem.Trigger);
                DispatchToUi(() =>
                {
                    LastMappedOutput = workItem.OutputLabel;
                    LastMappingStatus = $"Sent: {workItem.ButtonName} ({workItem.Trigger}) -> {workItem.OutputLabel}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send mapped output. token={workItem.SourceToken}, ex={ex.Message}");
                DispatchToUi(() => { LastMappingStatus = $"Error sending '{workItem.SourceToken}': {ex.Message}"; });
            }
        }
    }

    private static Key ParseKey(string? keyboardKey)
    {
        if (string.IsNullOrWhiteSpace(keyboardKey))
            return Key.None;

        var normalized = NormalizeKeyboardKeyToken(keyboardKey);

        if (Enum.TryParse<Key>(normalized, true, out var key))
            return key;

        try
        {
            var converter = new KeyConverter();
            var converted = converter.ConvertFromString(normalized);
            return converted is Key k ? k : Key.None;
        }
        catch
        {
            return Key.None;
        }
    }

    private static string NormalizeKeyboardKeyToken(string keyboardKey)
    {
        var token = keyboardKey.Trim();
        return KeyAliases.TryGetValue(token, out var alias) ? alias : token;
    }

    private void DispatchToUi(Action action)
    {
        if (action is null) return;
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    private void LoadTemplates(string? preferredProfileId = null)
    {
        AvailableTemplates.Clear();

        var templatesDir = _profileService.LoadTemplateDirectory();
        if (!Directory.Exists(templatesDir))
            return;

        var jsonFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly);
        var options = new System.Collections.Generic.List<TemplateOption>();

        foreach (var file in jsonFiles)
        {
            var profileId = Path.GetFileNameWithoutExtension(file);
            try
            {
                var template = _profileService.LoadTemplate(profileId);
                options.Add(new TemplateOption
                {
                    ProfileId = profileId,
                    GameId = template.GameId,
                    DisplayName = string.IsNullOrWhiteSpace(template.DisplayName) ? profileId : template.DisplayName
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load template '{profileId}': {ex.Message}");
            }
        }

        foreach (var opt in options.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase))
            AvailableTemplates.Add(opt);

        SelectedTemplate =
            (preferredProfileId is not null
                ? AvailableTemplates.FirstOrDefault(t => t.ProfileId == preferredProfileId)
                : null) ??
            AvailableTemplates.FirstOrDefault(t => t.ProfileId == _profileService.DefaultGameId) ??
            AvailableTemplates.FirstOrDefault(t => t.GameId == _profileService.DefaultGameId) ??
            AvailableTemplates.FirstOrDefault();
    }

    private void LoadSelectedTemplate()
    {
        if (SelectedTemplate is null)
            return;

        var template = _profileService.LoadTemplate(SelectedTemplate.ProfileId);

        CurrentTemplateDisplayName = template.DisplayName;

        Mappings.Clear();
        foreach (var mapping in template.Mappings)
            Mappings.Add(mapping);

        SelectedMapping = Mappings.FirstOrDefault();
        MappingCount = Mappings.Count;
    }

    [ObservableProperty]
    private ObservableCollection<TemplateOption> availableTemplates;

    [ObservableProperty]
    private TemplateOption? selectedTemplate;

    partial void OnSelectedTemplateChanged(TemplateOption? value)
    {
        // Reload the mappings whenever the user switches templates.
        try
        {
            LoadSelectedTemplate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load template '{value?.ProfileId ?? value?.GameId}': {ex.Message}");
        }
    }

    [ObservableProperty]
    private ObservableCollection<MappingEntry> mappings;

    [ObservableProperty]
    private ObservableCollection<string> availableGamepadButtons;

    [ObservableProperty]
    private ObservableCollection<TriggerMoment> availableTriggerModes;

    [ObservableProperty]
    private MappingEntry? selectedMapping;

    [ObservableProperty]
    private string currentTemplateDisplayName = string.Empty;

    [ObservableProperty]
    private int mappingCount;

    [ObservableProperty]
    private bool isRecordingKeyboardKey;

    [ObservableProperty]
    private string keyboardKeyCapturePrompt = string.Empty;

    [ObservableProperty]
    private string newBindingFromButton = "A";

    [ObservableProperty]
    private TriggerMoment newBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string newBindingKeyboardKey = string.Empty;

    [ObservableProperty]
    private string editBindingFromButton = "A";

    [ObservableProperty]
    private TriggerMoment editBindingTrigger = TriggerMoment.Tap;

    [ObservableProperty]
    private string editBindingKeyboardKey = string.Empty;

    [ObservableProperty]
    private string lastButtonPressed = string.Empty;

    [ObservableProperty]
    private string lastButtonReleased = string.Empty;

    [ObservableProperty]
    private string lastMappedOutput = "None";

    [ObservableProperty]
    private string lastMappingStatus = "Waiting for gamepad input";

    [ObservableProperty]
    private float leftThumbX;

    [ObservableProperty]
    private float leftThumbY;

    [ObservableProperty]
    private float rightThumbX;

    [ObservableProperty]
    private float rightThumbY;

    [ObservableProperty]
    private float leftTrigger;

    [ObservableProperty]
    private float rightTrigger;

    [ObservableProperty]
    private bool isGamepadRunning;

    [ObservableProperty]
    private string newProfileGameId = string.Empty;

    [ObservableProperty]
    private string newProfileDisplayName = string.Empty;

    public ProfileTemplatePanelViewModel ProfileTemplatePanel { get; }

    public NewBindingPanelViewModel NewBindingPanel { get; }

    public MappingEditorViewModel MappingEditorPanel { get; }

    public GamepadMonitorViewModel GamepadMonitorPanel { get; }

    public ProcessTargetPanelViewModel ProcessTargetPanel { get; }

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> recentProcesses;

    [ObservableProperty]
    private ProcessInfo? selectedTargetProcess;

    [ObservableProperty]
    private bool isProcessTargetingEnabled;

    [ObservableProperty]
    private string targetStatusText = "No target — keys sent globally";

    [RelayCommand]
    private void RefreshProcesses()
    {
        var current = _processTargetService.GetRecentWindowedProcesses();
        RecentProcesses.Clear();
        foreach (var p in current)
            RecentProcesses.Add(p);

        if (SelectedTargetProcess is not null)
        {
            var match = RecentProcesses.FirstOrDefault(p => p.ProcessId == SelectedTargetProcess.ProcessId)
                        ?? RecentProcesses.FirstOrDefault(p =>
                            string.Equals(p.ProcessName, SelectedTargetProcess.ProcessName,
                                StringComparison.OrdinalIgnoreCase));
            SelectedTargetProcess = match;
        }
    }

    [RelayCommand]
    private void ClearTargetProcess()
    {
        SelectedTargetProcess = null;
        IsProcessTargetingEnabled = false;
    }

    partial void OnSelectedTargetProcessChanged(ProcessInfo? value)
    {
        if (value is null)
        {
            IsProcessTargetingEnabled = false;
            TargetStatusText = "No target — keys sent globally";
        }
        else
        {
            IsProcessTargetingEnabled = true;
            TargetStatusText = $"Target selected: {value.ProcessName} (PID {value.ProcessId})";
            EnsureElevationCompatibility(value);
        }
    }

    private bool ShouldSendKeys()
    {
        if (!IsProcessTargetingEnabled || SelectedTargetProcess is null)
            return true;

        if (IsBlockedByUipi(SelectedTargetProcess))
        {
            TargetStatusText =
                $"Target requires admin privileges: {SelectedTargetProcess.ProcessName} (PID {SelectedTargetProcess.ProcessId})";
            return false;
        }

        var isForegroundMatch = _processTargetService.IsForeground(SelectedTargetProcess);
        var desiredStatus = isForegroundMatch
            ? $"Connected: {SelectedTargetProcess.ProcessName} (PID {SelectedTargetProcess.ProcessId})"
            : $"Waiting for target foreground: {SelectedTargetProcess.ProcessName} (PID {SelectedTargetProcess.ProcessId})";
        if (!string.Equals(TargetStatusText, desiredStatus, StringComparison.Ordinal))
            TargetStatusText = desiredStatus;

        return isForegroundMatch;
    }

    private bool IsBlockedByUipi(ProcessInfo target)
    {
        if (_isCurrentProcessElevated)
            return false;

        return _processTargetService.IsProcessElevated(target.ProcessId);
    }

    private void EnsureElevationCompatibility(ProcessInfo target)
    {
        if (!IsBlockedByUipi(target))
            return;

        if (_lastElevationPromptedProcessId == target.ProcessId)
            return;

        _lastElevationPromptedProcessId = target.ProcessId;
        var relaunch = MessageBox.Show(
            $"The selected target '{target.ProcessName}' is running as administrator.\n\n" +
            "This mapper is not elevated, so Windows UIPI can block injected input.\n\n" +
            "Relaunch this tool as administrator now?",
            "Administrator rights required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (relaunch == MessageBoxResult.Yes)
            RelaunchAsAdministrator();
    }

    private static void RelaunchAsAdministrator()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            Application.Current?.Shutdown();
        }
        catch (Win32Exception)
        {
            // User cancelled the UAC prompt.
        }
        catch
        {
            // Best-effort relaunch only.
        }
    }

    [RelayCommand]
    private void ReloadTemplate()
    {
        if (SelectedTemplate is null)
            return;

        LoadSelectedTemplate();
    }

    [RelayCommand]
    private void RecordKeyboardKey()
    {
        if (SelectedMapping is null) return;
        BeginKeyboardKeyCapture(
            KeyCaptureTarget.SelectedMapping,
            "Press a key to assign to the selected mapping (Esc to cancel).");
    }

    [RelayCommand]
    private void RecordNewBindingKey()
    {
        BeginKeyboardKeyCapture(
            KeyCaptureTarget.NewBinding,
            "Press a key for the new key binding (Esc to cancel).");
    }

    public void SetSelectedMappingKeyboardKey(Key key, Key? systemKey = null)
    {
        if (SelectedMapping is null) return;

        var recordedKey = key == Key.System && systemKey.HasValue ? systemKey.Value : key;
        if (recordedKey == Key.None || recordedKey == Key.System)
            return;

        var recorded = recordedKey.ToString();
        EditBindingKeyboardKey = recorded;
        SelectedMapping.KeyboardKey = recorded;
        IsRecordingKeyboardKey = false;
        KeyboardKeyCapturePrompt = string.Empty;
        _keyCaptureTarget = KeyCaptureTarget.None;
    }

    public void SetNewBindingKeyboardKey(Key key, Key? systemKey = null)
    {
        var recordedKey = key == Key.System && systemKey.HasValue ? systemKey.Value : key;
        if (recordedKey == Key.None || recordedKey == Key.System)
            return;

        NewBindingKeyboardKey = recordedKey.ToString();
        IsRecordingKeyboardKey = false;
        KeyboardKeyCapturePrompt = string.Empty;
        _keyCaptureTarget = KeyCaptureTarget.None;
    }

    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null)
    {
        if (!IsRecordingKeyboardKey)
            return false;

        if (_keyCaptureTarget == KeyCaptureTarget.SelectedMapping)
            SetSelectedMappingKeyboardKey(key, systemKey);
        else if (_keyCaptureTarget == KeyCaptureTarget.NewBinding)
            SetNewBindingKeyboardKey(key, systemKey);
        else
            return false;

        return true;
    }

    [RelayCommand]
    public void CancelKeyboardKeyRecording()
    {
        IsRecordingKeyboardKey = false;
        KeyboardKeyCapturePrompt = string.Empty;
        _keyCaptureTarget = KeyCaptureTarget.None;
    }

    [RelayCommand]
    private void CreateKeyBinding()
    {
        var button = (NewBindingFromButton ?? string.Empty).Trim();
        var keyToken = (NewBindingKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(button))
            return;

        var key = ParseKey(keyToken);
        var isMouseLookOutput = TryResolveMouseLookOutput(keyToken, out _);
        if (key == Key.None && !isMouseLookOutput)
            return;

        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button },
            KeyboardKey = isMouseLookOutput ? NormalizeKeyboardKeyToken(keyToken) : key.ToString(),
            Trigger = NewBindingTrigger,
            AnalogThreshold = null
        };

        Mappings.Add(entry);
        SelectedMapping = entry;
    }

    [RelayCommand]
    private void UpdateSelectedBinding()
    {
        if (SelectedMapping is null)
            return;

        var button = (EditBindingFromButton ?? string.Empty).Trim();
        var keyToken = (EditBindingKeyboardKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(button))
            return;

        var sourceType = SelectedMapping.From?.Type ?? GamepadBindingType.Button;
        if (sourceType == GamepadBindingType.Button &&
            !AvailableGamepadButtons.Any(b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase)))
            return;

        var key = ParseKey(keyToken);
        var isMouseLookOutput = TryResolveMouseLookOutput(keyToken, out _);
        if (key == Key.None && !isMouseLookOutput)
            return;

        SelectedMapping.From = new GamepadBinding { Type = sourceType, Value = button };
        SelectedMapping.Trigger = EditBindingTrigger;
        SelectedMapping.KeyboardKey = isMouseLookOutput ? NormalizeKeyboardKeyToken(keyToken) : key.ToString();
        SelectedMapping.AnalogThreshold = null;
    }

    [RelayCommand]
    private void AddMapping()
    {
        // Add a new default mapping (can be edited in the grid).
        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = "A" },
            KeyboardKey = "A",
            Trigger = TriggerMoment.Tap,
            AnalogThreshold = null
        };

        Mappings.Add(entry);
        SelectedMapping = entry;
    }

    [RelayCommand]
    private void RemoveSelectedMapping()
    {
        if (SelectedMapping is null) return;
        Mappings.Remove(SelectedMapping);
        SelectedMapping = Mappings.FirstOrDefault();
    }

    [RelayCommand]
    private void SaveProfile()
    {
        if (SelectedTemplate is null) return;

        var template = new GameProfileTemplate
        {
            SchemaVersion = 1,
            ProfileId = SelectedTemplate.ProfileId,
            GameId = SelectedTemplate.GameId,
            DisplayName = CurrentTemplateDisplayName,
            Mappings = Mappings.ToList()
        };

        _profileService.SaveTemplate(template);

        // Reload (this will re-populate mappings via OnSelectedTemplateChanged).
        LoadTemplates(template.ProfileId);
    }

    [RelayCommand]
    private void CreateProfile()
    {
        var gameId = (NewProfileGameId ?? string.Empty).Trim();
        var displayName = (NewProfileDisplayName ?? string.Empty).Trim();
        gameId = ProfileService.EnsureValidGameId(gameId);

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameId;

        var profileId = _profileService.CreateUniqueProfileId(gameId, displayName);

        var template = new GameProfileTemplate
        {
            SchemaVersion = 1,
            ProfileId = profileId,
            GameId = gameId,
            DisplayName = displayName,
            Mappings = new System.Collections.Generic.List<MappingEntry>()
        };

        _profileService.SaveTemplate(template, allowOverwrite: false);

        LoadTemplates(profileId);

        NewProfileGameId = string.Empty;
        NewProfileDisplayName = string.Empty;
    }

    [RelayCommand]
    private void DeleteSelectedProfile()
    {
        if (SelectedTemplate is null) return;
        var profileId = SelectedTemplate.ProfileId;

        if (string.Equals(profileId, _profileService.DefaultGameId, StringComparison.OrdinalIgnoreCase))
            return;

        var ok = MessageBox.Show(
            $"Delete profile '{SelectedTemplate.DisplayName}' ({SelectedTemplate.GameId})?",
            "Confirm delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (ok != MessageBoxResult.Yes) return;

        _profileService.DeleteTemplate(profileId);

        LoadTemplates();
    }

    private bool CanDeleteSelectedProfile()
        => SelectedTemplate is not null &&
           !string.Equals(SelectedTemplate.ProfileId, _profileService.DefaultGameId, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void StartGamepad()
    {
        if (IsGamepadRunning)
            return;

        _gamepadReader.Start();
        IsGamepadRunning = true;
    }

    [RelayCommand]
    private void StopGamepad()
    {
        if (!IsGamepadRunning)
            return;

        _gamepadReader.Stop();
        ForceReleaseAllHeldOutputs();
        ForceReleaseAnalogOutputs();
        IsGamepadRunning = false;
    }

    public void Dispose()
    {
        try
        {
            _gamepadReader.Stop();
            ForceReleaseAllHeldOutputs();
            ForceReleaseAnalogOutputs();

            _outputQueueCts.Cancel();
            _outputQueueSignal.Release();
            _outputQueueWorkerTask.Wait(500);
        }
        catch
        {
            // Best-effort shutdown.
        }
        finally
        {
            _outputQueueSignal.Dispose();
            _outputQueueCts.Dispose();
        }
    }

    partial void OnIsRecordingKeyboardKeyChanged(bool value)
    {
        if (!value)
        {
            KeyboardKeyCapturePrompt = string.Empty;
            _keyCaptureTarget = KeyCaptureTarget.None;
        }
    }

    partial void OnSelectedMappingChanged(MappingEntry? value)
    {
        if (value?.From is not null && value.From.Type == GamepadBindingType.Button)
        {
            var mappedButton = value.From.Value ?? string.Empty;
            EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault(
                b => string.Equals(b, mappedButton, StringComparison.OrdinalIgnoreCase))
                ?? (AvailableGamepadButtons.FirstOrDefault() ?? "A");
        }
        else
        {
            EditBindingFromButton = value?.From?.Value ?? string.Empty;
        }

        EditBindingTrigger = value?.Trigger ?? TriggerMoment.Tap;
        EditBindingKeyboardKey = value?.KeyboardKey ?? string.Empty;
    }

    private void BeginKeyboardKeyCapture(KeyCaptureTarget target, string prompt)
    {
        _keyCaptureTarget = target;
        IsRecordingKeyboardKey = true;
        KeyboardKeyCapturePrompt = prompt;
    }
}

