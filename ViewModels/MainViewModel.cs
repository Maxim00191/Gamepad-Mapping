using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private enum KeyCaptureTarget
    {
        None,
        SelectedMapping,
        NewBinding
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

    private readonly TriggerMoment _buttonPressedTrigger = TriggerMoment.Pressed;
    private readonly TriggerMoment _buttonReleasedTrigger = TriggerMoment.Released;
    private readonly TriggerMoment _buttonTapTrigger = TriggerMoment.Tap;
    private KeyCaptureTarget _keyCaptureTarget = KeyCaptureTarget.None;

    public MainViewModel()
    {
        // Ensure property updates land on the UI thread (gamepad events originate on a background thread).
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _profileService = new ProfileService();
        _gamepadReader = new GamepadReader();
        _keyboardEmulator = new KeyboardEmulator();

        AvailableTemplates = new ObservableCollection<TemplateOption>();
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
            });
        _gamepadReader.OnRightThumbstickChanged += v =>
            DispatchToUi(() =>
            {
                RightThumbX = v.X;
                RightThumbY = v.Y;
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

        StartGamepad();
    }

    private void ApplyButtonMappings(GamepadButtons buttons, TriggerMoment trigger)
    {
        // Only supports button->keyboard mappings for now.
        var buttonName = buttons.ToString();
        var snapshot = Mappings.ToList();

        foreach (var mapping in snapshot)
        {
            if (mapping?.From is null) continue;
            if (mapping.From.Type != GamepadBindingType.Button) continue;
            if (!string.Equals(mapping.From.Value, buttonName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (mapping.Trigger != trigger) continue;

            var key = ParseKey(mapping.KeyboardKey);
            if (key == Key.None) continue;

            try
            {
                if (trigger == TriggerMoment.Pressed)
                {
                    _keyboardEmulator.KeyDown(key);
                }
                else if (trigger == TriggerMoment.Released)
                {
                    _keyboardEmulator.KeyUp(key);
                }
                else if (trigger == TriggerMoment.Tap)
                {
                    _keyboardEmulator.TapKey(key);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send key mapping. key={mapping.KeyboardKey}, ex={ex.Message}");
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
        if (key == Key.None)
            return;

        var entry = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button },
            KeyboardKey = key.ToString(),
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

        if (!AvailableGamepadButtons.Any(b => string.Equals(b, button, StringComparison.OrdinalIgnoreCase)))
            return;

        var key = ParseKey(keyToken);
        if (key == Key.None)
            return;

        SelectedMapping.From = new GamepadBinding { Type = GamepadBindingType.Button, Value = button };
        SelectedMapping.Trigger = EditBindingTrigger;
        SelectedMapping.KeyboardKey = key.ToString();
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
        IsGamepadRunning = false;
    }

    public void Dispose()
    {
        try
        {
            _gamepadReader.Stop();
        }
        catch
        {
            // Best-effort shutdown.
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
            EditBindingFromButton = AvailableGamepadButtons.FirstOrDefault() ?? "A";
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

