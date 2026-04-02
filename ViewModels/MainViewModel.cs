using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using Gamepad_Mapping.Views;
using ElevationHandlerService = GamepadMapperGUI.Utils.ElevationHandler;
using Vortice.XInput;

namespace Gamepad_Mapping.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly UiLanguageOption[] SupportedUiLanguages =
    [
        new("zh-CN", "简体中文"),
        new("en-US", "English")
    ];

    private readonly Dispatcher _dispatcher;
    private readonly IProfileService _profileService;
    private readonly IGamepadReader _gamepadReader;
    private readonly IProcessTargetService _processTargetService;
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private readonly IElevationHandler _elevationHandler;
    private readonly IAppStatusMonitor _appStatusMonitor;
    private readonly IMappingEngine _mappingEngine;
    private readonly EventHandler _profilesLoadedHandler;
    private readonly EventHandler<AppStatusChangedEventArgs> _appStatusChangedHandler;
    private IReadOnlyList<MappingEntry> _mappingsSnapshot = Array.Empty<MappingEntry>();
    /// <summary>From template JSON; preserved when saving so <c>comboLeadButtons</c> is not stripped.</summary>
    private List<string>? _comboLeadButtonsPersist;

    /// <summary>Last loaded template <see cref="GameProfileTemplate.TemplateGroupId"/>; used to carry <c>targetProcessName</c> across related profiles.</summary>
    private string? _lastLoadedTemplateGroupIdForTargetInherit;
    private ComboHudWindow? _comboHudWindow;
    private readonly AppSettings _appSettings;
    private readonly ISettingsService _settingsService;
    private DispatcherTimer? _templateSwitchHudTimer;
    private bool _isTemplateSwitchHudActive;
    private bool _isInitializingUiLanguageSelection;

    public MainViewModel(
        IProfileService? profileService = null,
        IGamepadReader? gamepadReader = null,
        IProcessTargetService? processTargetService = null,
        IKeyboardCaptureService? keyboardCaptureService = null,
        IElevationHandler? elevationHandler = null,
        IAppStatusMonitor? appStatusMonitor = null,
        IMappingEngine? mappingEngine = null,
        ISettingsService? settingsService = null)
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _profileService = profileService ?? new ProfileService();
        _settingsService = settingsService ?? new SettingsService();

        _appSettings = _settingsService.LoadSettings();
        ModifierGraceMsSetting = _appSettings.ModifierGraceMs;
        LeadKeyReleaseSuppressMsSetting = _appSettings.LeadKeyReleaseSuppressMs;
        GamepadPollingIntervalMs = _appSettings.GamepadPollingIntervalMs;
        DefaultAnalogActivationThreshold = _appSettings.DefaultAnalogActivationThreshold;
        MouseLookSensitivity = _appSettings.MouseLookSensitivity;
        AnalogChangeEpsilon = _appSettings.AnalogChangeEpsilon;
        KeyboardTapHoldDurationMs = _appSettings.KeyboardTapHoldDurationMs;
        TapInterKeyDelayMs = _appSettings.TapInterKeyDelayMs;
        TextInterCharDelayMs = _appSettings.TextInterCharDelayMs;
        ComboHudPlacementSetting = ParseComboHudPlacement(_appSettings.ComboHudPlacement);
        AvailableUiLanguages = new ObservableCollection<UiLanguageOption>(SupportedUiLanguages);
        _isInitializingUiLanguageSelection = true;
        SelectedUiLanguage =
            AvailableUiLanguages.FirstOrDefault(x =>
                string.Equals(x.CultureName, _appSettings.UiCulture, StringComparison.OrdinalIgnoreCase))
            ?? AvailableUiLanguages.FirstOrDefault(x =>
                string.Equals(x.CultureName, "zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? AvailableUiLanguages.FirstOrDefault();
        _isInitializingUiLanguageSelection = false;

        if (SelectedUiLanguage is not null)
            ApplyUiLanguage(SelectedUiLanguage.CultureName, persist: false);

        var baseDeadzone = _appSettings.ThumbstickDeadzone;
        static float ResolveStickDeadzone(float specific, float shared)
        {
            var value = specific > 0f ? specific : shared;
            return Math.Clamp(value, 0f, 0.9f);
        }

        var initialLeftDeadzone = ResolveStickDeadzone(_appSettings.LeftThumbstickDeadzone, baseDeadzone);
        var initialRightDeadzone = ResolveStickDeadzone(_appSettings.RightThumbstickDeadzone, baseDeadzone);

        _gamepadReader = gamepadReader ??
            new GamepadReader(
                initialLeftDeadzone,
                initialRightDeadzone,
                _appSettings.LeftTriggerInnerDeadzone,
                _appSettings.LeftTriggerOuterDeadzone,
                _appSettings.RightTriggerInnerDeadzone,
                _appSettings.RightTriggerOuterDeadzone);
        _processTargetService = processTargetService ?? new ProcessTargetService();
        _keyboardCaptureService = keyboardCaptureService ?? new KeyboardCaptureService();
        _elevationHandler = elevationHandler ?? new ElevationHandlerService(_processTargetService);
        _appStatusMonitor = appStatusMonitor ?? new AppStatusMonitor(_processTargetService, _elevationHandler);
        _profilesLoadedHandler = (_, _) => OnPropertyChanged(nameof(AvailableTemplates));
        _appStatusChangedHandler = (_, args) =>
            DispatchToUi(() =>
            {
                TargetState = args.State;
                TargetStatusText = args.StatusText;
            });

        AvailableTemplates = _profileService.AvailableTemplates;
        Mappings = new ObservableCollection<MappingEntry>();
        AvailableGamepadButtons = new ObservableCollection<string>(
            Enum.GetNames<GamepadButtons>().Where(n => !string.Equals(n, nameof(GamepadButtons.None), StringComparison.OrdinalIgnoreCase)));
        AvailableTriggerModes = new ObservableCollection<TriggerMoment>(Enum.GetValues<TriggerMoment>());
        Mappings.CollectionChanged += (_, _) =>
        {
            MappingCount = Mappings.Count;
            _mappingsSnapshot = Mappings.ToList();
        };

        ProfileTemplatePanel = new ProfileTemplatePanelViewModel(this);
        NewBindingPanel = new NewBindingPanelViewModel(this);
        MappingEditorPanel = new MappingEditorViewModel(this);
        GamepadMonitorPanel = new GamepadMonitorViewModel(
            StopGamepadCommand,
            StartGamepadCommand,
            OnHudEnabledChanged,
            initialLeftThumbstickDeadzone: initialLeftDeadzone,
            initialRightThumbstickDeadzone: initialRightDeadzone,
            deadzoneChanged: OnThumbstickDeadzoneChanged,
            initialLeftTriggerInnerDeadzone: _appSettings.LeftTriggerInnerDeadzone,
            initialLeftTriggerOuterDeadzone: _appSettings.LeftTriggerOuterDeadzone,
            initialRightTriggerInnerDeadzone: _appSettings.RightTriggerInnerDeadzone,
            initialRightTriggerOuterDeadzone: _appSettings.RightTriggerOuterDeadzone,
            triggerDeadzonesChanged: OnTriggerDeadzonesChanged,
            initialComboHudPanelAlpha: Math.Clamp(_appSettings.ComboHudPanelAlpha, 24, 220),
            initialComboHudShadowOpacity: Math.Clamp(_appSettings.ComboHudShadowOpacity, 0.08, 0.60),
            comboHudChromeChanged: OnComboHudChromeChanged,
            uiDispatcher: _dispatcher);
        ProcessTargetPanel = new ProcessTargetPanelViewModel(this);
        ProfileTemplatePanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        NewBindingPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        MappingEditorPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;

        Func<string>? comboHudGateMessageFactory = null;
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            comboHudGateMessageFactory = () => loc["ComboHudGateHint"];

        _mappingEngine = mappingEngine ?? new MappingEngine(
            new KeyboardEmulator(),
            new MouseEmulator(),
            () => _appStatusMonitor.CanSendOutput,
            a => DispatchToUi(a),
            value => GamepadMonitorPanel.LastMappedOutput = value,
            value => GamepadMonitorPanel.LastMappingStatus = value,
            OnComboHud,
            _profileService.ModifierGraceMs,
            _profileService.LeadKeyReleaseSuppressMs,
            requestTemplateSwitchToProfileId: pid => DispatchToUi(() => ApplyTemplateSwitchFromGamepad(pid)),
            setComboHudGateHint: s => DispatchToUi(() => GamepadMonitorPanel.ComboHudGateHint = s ?? string.Empty),
            comboHudGateMessageFactory: comboHudGateMessageFactory,
            isComboHudPresentationSuppressed: () => _isTemplateSwitchHudActive);
        _profileService.ProfilesLoaded += _profilesLoadedHandler;
        _appStatusMonitor.StatusChanged += _appStatusChangedHandler;

        _gamepadReader.OnInputFrame += frame =>
        {
            var allow = _appStatusMonitor.EvaluateNow();
            float leftDz;
            float rightDz;
            if (_gamepadReader is GamepadReader gr)
            {
                leftDz = gr.LeftThumbstickDeadzone;
                rightDz = gr.RightThumbstickDeadzone;
            }
            else
            {
                leftDz = GamepadMonitorPanel.LeftThumbstickDeadzone;
                rightDz = GamepadMonitorPanel.RightThumbstickDeadzone;
            }

            var result = _mappingEngine.ProcessInputFrame(frame, _mappingsSnapshot, allow);
            GamepadMonitorPanel.RecordInputFrameSnapshot(frame, result, leftDz, rightDz);
        };

        SelectedTemplate = _profileService.ReloadTemplates(_profileService.LastSelectedTemplateProfileId);
        LoadSelectedTemplate();

        StartGamepad();
    }

    [ObservableProperty]
    private ObservableCollection<TemplateOption> availableTemplates;

    [ObservableProperty]
    private TemplateOption? selectedTemplate;

    partial void OnSelectedTemplateChanged(TemplateOption? value)
    {
        try
        {
            App.Logger.Info($"Switching to template: {value?.DisplayName} ({value?.ProfileId})");
            LoadSelectedTemplate();
            _profileService.PersistLastSelectedTemplateProfileId(value?.ProfileId);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to load template '{value?.ProfileId ?? value?.TemplateGroupId}'", ex);
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
    private bool isGamepadRunning;

    public ProfileTemplatePanelViewModel ProfileTemplatePanel { get; }

    public NewBindingPanelViewModel NewBindingPanel { get; }

    public MappingEditorViewModel MappingEditorPanel { get; }

    public GamepadMonitorViewModel GamepadMonitorPanel { get; }

    public ProcessTargetPanelViewModel ProcessTargetPanel { get; }

    public IKeyboardCaptureService KeyboardCaptureService => _keyboardCaptureService;

    /// <summary>User-declared executable name (profile field); edit here or in JSON, then save profile.</summary>
    [ObservableProperty]
    private string templateTargetProcessName = string.Empty;

    [ObservableProperty]
    private ProcessInfo? selectedTargetProcess;

    [ObservableProperty]
    private bool isProcessTargetingEnabled;

    [ObservableProperty]
    private string targetStatusText = "No target selected - output suppressed";

    [ObservableProperty]
    private AppTargetingState targetState = AppTargetingState.NoTargetSelected;

    partial void OnTemplateTargetProcessNameChanged(string value)
    {
        ApplyDeclaredProcessTarget();
    }

    private void ApplyDeclaredProcessTarget()
    {
        var raw = TemplateTargetProcessName;
        if (string.IsNullOrWhiteSpace(raw))
        {
            SelectedTargetProcess = null;
            return;
        }

        SelectedTargetProcess = _processTargetService.CreateTargetFromDeclaredProcessName(raw);
    }

    partial void OnSelectedTargetProcessChanged(ProcessInfo? value)
    {
        if (value is null)
        {
            IsProcessTargetingEnabled = false;
        }
        else
        {
            IsProcessTargetingEnabled = true;
            _elevationHandler.CheckAndPromptElevation(value);
        }

        _appStatusMonitor.UpdateTarget(SelectedTargetProcess, IsProcessTargetingEnabled);
    }

    partial void OnIsProcessTargetingEnabledChanged(bool value)
    {
        _appStatusMonitor.UpdateTarget(SelectedTargetProcess, value);
    }

    [ObservableProperty]
    private int modifierGraceMsSetting;

    partial void OnModifierGraceMsSettingChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 10_000);
        if (clamped != value)
            modifierGraceMsSetting = clamped;
        _appSettings.ModifierGraceMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int leadKeyReleaseSuppressMsSetting;

    partial void OnLeadKeyReleaseSuppressMsSettingChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 10_000);
        if (clamped != value)
            leadKeyReleaseSuppressMsSetting = clamped;
        _appSettings.LeadKeyReleaseSuppressMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int gamepadPollingIntervalMs;

    partial void OnGamepadPollingIntervalMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 5, 30);
        if (clamped != value)
            gamepadPollingIntervalMs = clamped;
        _appSettings.GamepadPollingIntervalMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private float defaultAnalogActivationThreshold;

    partial void OnDefaultAnalogActivationThresholdChanged(float value)
    {
        var clamped = Math.Clamp(value, 0.01f, 1f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            defaultAnalogActivationThreshold = clamped;
        _appSettings.DefaultAnalogActivationThreshold = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private float mouseLookSensitivity;

    partial void OnMouseLookSensitivityChanged(float value)
    {
        var clamped = Math.Clamp(value, 1f, 100f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            mouseLookSensitivity = clamped;
        _appSettings.MouseLookSensitivity = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private float analogChangeEpsilon;

    partial void OnAnalogChangeEpsilonChanged(float value)
    {
        var clamped = Math.Clamp(value, 0.001f, 0.1f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            analogChangeEpsilon = clamped;
        _appSettings.AnalogChangeEpsilon = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int keyboardTapHoldDurationMs;

    partial void OnKeyboardTapHoldDurationMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 20, 50);
        if (clamped != value)
            keyboardTapHoldDurationMs = clamped;
        _appSettings.KeyboardTapHoldDurationMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int tapInterKeyDelayMs;

    partial void OnTapInterKeyDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1000);
        if (clamped != value)
            tapInterKeyDelayMs = clamped;
        _appSettings.TapInterKeyDelayMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int textInterCharDelayMs;

    partial void OnTextInterCharDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1000);
        if (clamped != value)
            textInterCharDelayMs = clamped;
        _appSettings.TextInterCharDelayMs = clamped;
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private ComboHudPlacement comboHudPlacementSetting;

    partial void OnComboHudPlacementSettingChanged(ComboHudPlacement value)
    {
        _appSettings.ComboHudPlacement = value.ToString();
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private ObservableCollection<UiLanguageOption> availableUiLanguages = [];

    [ObservableProperty]
    private UiLanguageOption? selectedUiLanguage;

    partial void OnSelectedUiLanguageChanged(UiLanguageOption? value)
    {
        if (value is null)
            return;

        ApplyUiLanguage(value.CultureName, persist: !_isInitializingUiLanguageSelection);
        ReloadLocalizedTemplateContent();
    }

    private void ApplyTemplateSwitchFromGamepad(string targetProfileId)
    {
        var id = (targetProfileId ?? string.Empty).Trim();
        if (id.Length == 0 || !_profileService.TemplateExists(id))
            return;

        var opt = _profileService.AvailableTemplates.FirstOrDefault(t =>
            string.Equals(t.ProfileId, id, StringComparison.OrdinalIgnoreCase));
        if (opt is null)
            return;

        if (string.Equals(SelectedTemplate?.ProfileId, opt.ProfileId, StringComparison.OrdinalIgnoreCase))
            return;

        // Before ForceRelease: Sync() invokes OnComboHud(null) synchronously on the UI thread; guard must be on first.
        if (GamepadMonitorPanel.IsHudEnabled)
        {
            _isTemplateSwitchHudActive = true;
            _mappingEngine.InvalidateComboHudPresentation();
        }

        _mappingEngine.ForceReleaseAllOutputs();
        _mappingEngine.ForceReleaseAnalogOutputs();
        SelectedTemplate = opt;

        if (GamepadMonitorPanel.IsHudEnabled)
            ShowTemplateSwitchHud(opt.DisplayName);
    }

    [RelayCommand(CanExecute = nameof(CanStartGamepad))]
    private void StartGamepad()
    {
        if (IsGamepadRunning)
            return;

        _gamepadReader.Start();
        IsGamepadRunning = true;
        GamepadMonitorPanel.IsGamepadRunning = true;
    }

    private bool CanStartGamepad() => !IsGamepadRunning;

    [RelayCommand(CanExecute = nameof(CanStopGamepad))]
    private void StopGamepad()
    {
        if (!IsGamepadRunning)
            return;

        _gamepadReader.Stop();
        _mappingEngine.ForceReleaseAllOutputs();
        _mappingEngine.ForceReleaseAnalogOutputs();
        IsGamepadRunning = false;
        GamepadMonitorPanel.IsGamepadRunning = false;
    }

    private bool CanStopGamepad() => IsGamepadRunning;

    partial void OnIsGamepadRunningChanged(bool value)
    {
        StartGamepadCommand.NotifyCanExecuteChanged();
        StopGamepadCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        try
        {
            _gamepadReader.Stop();
            if (_comboHudWindow is not null)
            {
                _comboHudWindow.Close();
                _comboHudWindow = null;
            }
            _mappingEngine.Dispose();
            GamepadMonitorPanel.Dispose();
            _appStatusMonitor.StatusChanged -= _appStatusChangedHandler;
            _appStatusMonitor.Dispose();
            _profileService.ProfilesLoaded -= _profilesLoadedHandler;
        }
        catch
        {
            // Best-effort shutdown.
        }
    }

    partial void OnSelectedMappingChanged(MappingEntry? value)
    {
        MappingEditorPanel.SyncFromSelection(value);
    }

    private void DispatchToUi(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (action is null) return;
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action, priority);
    }

    private void OnComboHud(ComboHudContent? content)
    {
        DispatchToUi(() =>
        {
            if (_isTemplateSwitchHudActive)
                return;

            if (!GamepadMonitorPanel.IsHudEnabled)
            {
                _comboHudWindow?.HideHud();
                return;
            }

            if (content is null)
            {
                _comboHudWindow?.HideHud();
                return;
            }

            _comboHudWindow ??= new ComboHudWindow();
            var a = (byte)Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220);
            var o = Math.Clamp(GamepadMonitorPanel.ComboHudShadowOpacity, 0.08, 0.60);
            _comboHudWindow.ShowHud(content, a, o, ComboHudPlacementSetting);
        }, DispatcherPriority.Input);
    }

    private void ShowTemplateSwitchHud(string profileDisplayName)
    {
        if (!GamepadMonitorPanel.IsHudEnabled)
        {
            _isTemplateSwitchHudActive = false;
            return;
        }

        if (_templateSwitchHudTimer is not null)
        {
            _templateSwitchHudTimer.Stop();
            _templateSwitchHudTimer = null;
        }

        _mappingEngine.InvalidateComboHudPresentation();

        _templateSwitchHudTimer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3)
        };

        _templateSwitchHudTimer.Tick += (_, _) =>
        {
            _templateSwitchHudTimer?.Stop();
            _templateSwitchHudTimer = null;
            _isTemplateSwitchHudActive = false;
            _mappingEngine.InvalidateComboHudPresentation();
            _mappingEngine.RefreshComboHud();
        };

        _comboHudWindow ??= new ComboHudWindow();
        var a = (byte)Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220);
        var o = Math.Clamp(GamepadMonitorPanel.ComboHudShadowOpacity, 0.08, 0.60);

        var title = "Profile switched";
        var line = new ComboHudLine($"→ {profileDisplayName}", null);
        var content = new ComboHudContent(title, new[] { line });
        _comboHudWindow.ShowHud(content, a, o, ComboHudPlacementSetting);

        _templateSwitchHudTimer.Start();
    }

    private void OnComboHudChromeChanged(int panelAlpha, double shadowOpacity)
    {
        var a = Math.Clamp(panelAlpha, 24, 220);
        var o = Math.Clamp(shadowOpacity, 0.08, 0.60);
        _appSettings.ComboHudPanelAlpha = a;
        _appSettings.ComboHudShadowOpacity = o;
        _settingsService.SaveSettings(_appSettings);

        DispatchToUi(() =>
        {
            if (_comboHudWindow is { IsVisible: true })
                _comboHudWindow.ApplyVisualSettings((byte)a, o);
        });
    }

    private void OnHudEnabledChanged(bool isEnabled)
    {
        if (!isEnabled)
            DispatchToUi(() =>
            {
                if (_templateSwitchHudTimer is not null)
                {
                    _templateSwitchHudTimer.Stop();
                    _templateSwitchHudTimer = null;
                }

                _isTemplateSwitchHudActive = false;
                GamepadMonitorPanel.ComboHudGateHint = string.Empty;
                _comboHudWindow?.HideHud();
            });
    }

    private void LoadSelectedTemplate()
    {
        var template = _profileService.LoadSelectedTemplate(SelectedTemplate);
        if (template is null)
            return;

        CurrentTemplateDisplayName = template.DisplayName;

        _comboLeadButtonsPersist = template.ComboLeadButtons?.ToList();
        _mappingEngine.SetComboLeadButtonsFromTemplate(template.ComboLeadButtons);

        Mappings.Clear();
        foreach (var mapping in template.Mappings)
            Mappings.Add(mapping);

        _mappingsSnapshot = Mappings.ToList();

        SelectedMapping = Mappings.FirstOrDefault();
        MappingCount = Mappings.Count;

        var fromFile = (template.TargetProcessName ?? string.Empty).Trim();
        var uiBefore = (TemplateTargetProcessName ?? string.Empty).Trim();

        if (fromFile.Length > 0)
            TemplateTargetProcessName = fromFile;
        else if (uiBefore.Length > 0
                 && ProfileService.ProfilesLikelyShareGameExecutable(_lastLoadedTemplateGroupIdForTargetInherit, template.TemplateGroupId))
        {
            TemplateTargetProcessName = uiBefore;
            template.TargetProcessName = uiBefore;
            _profileService.SaveTemplate(template);
        }
        else
            TemplateTargetProcessName = string.Empty;

        _lastLoadedTemplateGroupIdForTargetInherit = template.TemplateGroupId;

        ApplyDeclaredProcessTarget();
        UpdateTemplateToggleDisplayNames();
    }

    /// <summary>Combo lead names from the loaded template; written back unchanged on Save profile.</summary>
    public IReadOnlyList<string>? ComboLeadButtonsPersist => _comboLeadButtonsPersist;

    public IProfileService GetProfileService() => _profileService;

    public void RefreshTemplates(string? preferredProfileId = null)
    {
        SelectedTemplate = _profileService.ReloadTemplates(preferredProfileId);
    }

    public void ReloadSelectedTemplate() => LoadSelectedTemplate();

    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null)
        => _keyboardCaptureService.TryCaptureKeyboardKey(key, systemKey);

    public void CancelKeyboardKeyRecording()
        => _keyboardCaptureService.CancelCapture();

    private void OnChildPanelConfigurationChanged(object? sender, EventArgs e)
    {
        MappingCount = Mappings.Count;
    }

    private void OnThumbstickDeadzoneChanged(float left, float right)
    {
        if (_gamepadReader is GamepadReader concreteReader)
        {
            concreteReader.LeftThumbstickDeadzone = left;
            concreteReader.RightThumbstickDeadzone = right;
        }

        _appSettings.LeftThumbstickDeadzone = left;
        _appSettings.RightThumbstickDeadzone = right;
        _settingsService.SaveSettings(_appSettings);
    }

    private void OnTriggerDeadzonesChanged(float leftInner, float leftOuter, float rightInner, float rightOuter)
    {
        if (_gamepadReader is GamepadReader concreteReader)
        {
            concreteReader.LeftTriggerInnerDeadzone = leftInner;
            concreteReader.LeftTriggerOuterDeadzone = leftOuter;
            concreteReader.RightTriggerInnerDeadzone = rightInner;
            concreteReader.RightTriggerOuterDeadzone = rightOuter;
        }

        _appSettings.LeftTriggerInnerDeadzone = leftInner;
        _appSettings.LeftTriggerOuterDeadzone = leftOuter;
        _appSettings.RightTriggerInnerDeadzone = rightInner;
        _appSettings.RightTriggerOuterDeadzone = rightOuter;
        _settingsService.SaveSettings(_appSettings);
    }

    private static ComboHudPlacement ParseComboHudPlacement(string? raw)
    {
        return Enum.TryParse<ComboHudPlacement>(raw, true, out var parsed)
            ? parsed
            : ComboHudPlacement.BottomRight;
    }

    private void ReloadLocalizedTemplateContent()
    {
        var selectedProfileId = SelectedTemplate?.ProfileId;
        var reselected = _profileService.ReloadTemplates(selectedProfileId);
        OnPropertyChanged(nameof(AvailableTemplates));
        if (reselected is not null)
            SelectedTemplate = reselected;
        else
            UpdateTemplateToggleDisplayNames();
    }

    private void UpdateTemplateToggleDisplayNames()
    {
        if (Mappings is null || Mappings.Count == 0)
            return;

        foreach (var mapping in Mappings)
        {
            if (mapping.TemplateToggle is null)
            {
                mapping.TemplateToggleDisplayName = string.Empty;
                continue;
            }

            var targetId = mapping.TemplateToggle.AlternateProfileId?.Trim() ?? string.Empty;
            var localizedName = AvailableTemplates.FirstOrDefault(t =>
                string.Equals(t.ProfileId, targetId, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? string.Empty;
            mapping.TemplateToggleDisplayName = localizedName;
        }
    }

    private void ApplyUiLanguage(string cultureName, bool persist)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo("zh-CN");
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (Application.Current?.Resources["Loc"] is TranslationService translationService)
            translationService.Culture = culture;

        if (!persist)
            return;

        if (!string.Equals(_appSettings.UiCulture, culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            _appSettings.UiCulture = culture.Name;
            _settingsService.SaveSettings(_appSettings);
        }
    }
}

