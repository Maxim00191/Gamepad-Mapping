using System;
using System.ComponentModel;
using GamepadMapperGUI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Gamepad_Mapping.Utils;
using ElevationHandlerService = GamepadMapperGUI.Utils.ElevationHandler;

namespace Gamepad_Mapping.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly IUiSynchronization _uiSync;
    private readonly IProfileService _profileService;
    private readonly IGamepadService _gamepadService;
    private readonly IMappingManager _mappingManager;
    private readonly IUiOrchestrator _uiOrchestrator;
    private readonly IProcessTargetService _processTargetService;
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private readonly IElevationHandler _elevationHandler;
    private readonly IAppStatusMonitor _appStatusMonitor;
    private readonly ISettingsService _settingsService;
    private readonly SettingsOrchestrator _settingsOrchestrator;
    private readonly ProfileOrchestrator _profileOrchestrator;
    private readonly IInputEmulationStackFactory _inputEmulationStackFactory;
    private readonly IRadialMenuHud _radialMenuHud;
    private readonly Debouncer _processNameDebouncer;
    private bool _suppressGamepadMonitorSettingsPersistence;

    private readonly ICommunityTemplateService _communityService;
    private readonly IUpdateService _updateService;
    private readonly ILocalFileService _localFileService;
    private readonly IUpdateInstallerService _updateInstallerService;
    private readonly IUpdateNotificationService _updateNotificationService;
    private readonly IAppToastService _appToastService;
    private readonly AppToastViewModel _toastHost;
    private readonly IKeyboardEmulator? _keyboardEmulatorOverride;
    private readonly IMouseEmulator? _mouseEmulatorOverride;

    public UpdateViewModel UpdatePanel { get; }
    public AppToastViewModel ToastHost => _toastHost;

    public MainViewModel(
        IProfileService? profileService = null,
        IGamepadReader? gamepadReader = null,
        IProcessTargetService? processTargetService = null,
        IKeyboardCaptureService? keyboardCaptureService = null,
        IElevationHandler? elevationHandler = null,
        IAppStatusMonitor? appStatusMonitor = null,
        IMappingEngine? mappingEngine = null,
        ISettingsService? settingsService = null,
        ICommunityTemplateService? communityService = null,
        IUpdateService? updateService = null,
        IGitHubContentService? gitHubContentService = null,
        ILocalFileService? localFileService = null,
        IUpdateInstallerService? updateInstallerService = null,
        IUpdateQuotaService? updateQuotaService = null,
        ITrustedUtcTimeService? trustedUtcTimeService = null,
        IUpdateVersionCacheService? updateVersionCacheService = null,
        IUpdateQuotaPolicyProvider? updateQuotaPolicyProvider = null,
        IUpdateNotificationService? updateNotificationService = null,
        IAppToastService? appToastService = null,
        IKeyboardEmulator? keyboardEmulator = null,
        IMouseEmulator? mouseEmulator = null,
        IXInput? xinput = null,
        IGamepadSource? gamepadSource = null,
        IInputEmulationStackFactory? inputEmulationStackFactory = null,
        IRadialMenuHud? radialMenuHud = null)
    {
        if ((keyboardEmulator is null) != (mouseEmulator is null))
            throw new ArgumentException("keyboardEmulator and mouseEmulator must both be supplied or both omitted.");

        _keyboardEmulatorOverride = keyboardEmulator;
        _mouseEmulatorOverride = mouseEmulator;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _uiSync = new DispatcherUiSynchronization(_dispatcher);
        _radialMenuHud = radialMenuHud
            ?? (mappingEngine is null
                ? new RadialMenuHudPresenter(
                    () => (RadialMenuHudLabelMode)RadialMenuHudLabelModeIndex,
                    () => (int)GamepadMonitorPanel.ComboHudPanelAlpha)
                : NullRadialMenuHud.Instance);
        _settingsService = settingsService ?? new SettingsService();
        _settingsOrchestrator = new SettingsOrchestrator(_settingsService);
        _inputEmulationStackFactory = inputEmulationStackFactory ?? new InputEmulationStackFactory();
        var appSettings = _settingsOrchestrator.Settings;

        _processNameDebouncer = new Debouncer(TimeSpan.FromMilliseconds(1000));

        _profileService = profileService ?? new ProfileService(settingsService: _settingsService, appSettings: appSettings);
        _processTargetService = processTargetService ?? new ProcessTargetService();
        _profileOrchestrator = new ProfileOrchestrator(_profileService, _processTargetService);
        _profileOrchestrator.TemplateLoaded += OnTemplateLoaded;
        _profileOrchestrator.TemplateSwitchRequested += ShowTemplateSwitchHud;

        _localFileService = localFileService ?? new LocalFileService();
        var sharedGitHubContentService = gitHubContentService ?? new GitHubContentService();
        var resolvedUpdateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();
        _communityService = communityService ?? new CommunityTemplateService(_profileService, sharedGitHubContentService, _localFileService);
        _updateService = updateService ?? new UpdateService(sharedGitHubContentService, _settingsService, appSettings, resolvedUpdateVersionCacheService);
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();
        _updateNotificationService = updateNotificationService ?? new UpdateNotificationService();
        _appToastService = appToastService ?? new AppToastService();
        _toastHost = new AppToastViewModel(_appToastService);
        
        _uiOrchestrator = new UiOrchestrator(_appToastService, _dispatcher);

        UpdatePanel = new UpdateViewModel(
            _updateService,
            _settingsService,
            appSettings,
            _localFileService,
            _updateInstallerService,
            updateQuotaService ?? new UpdateQuotaService(updateQuotaPolicyProvider ?? new StaticUpdateQuotaPolicyProvider(), trustedUtcTimeService ?? new TrustedUtcTimeService()),
            resolvedUpdateVersionCacheService);

        var initialLeftDeadzone = ResolveStickDeadzone(appSettings.LeftThumbstickDeadzone, appSettings.ThumbstickDeadzone);
        var initialRightDeadzone = ResolveStickDeadzone(appSettings.RightThumbstickDeadzone, appSettings.ThumbstickDeadzone);

        var dzShape = ThumbstickDeadzoneShapeParser.Parse(appSettings.ThumbstickDeadzoneShape);
        var reader = gamepadReader ?? new GamepadReader(
            gamepadSource ?? new XInputSource(xinput ?? new XInputService()),
            initialLeftDeadzone,
            initialRightDeadzone,
            appSettings.LeftTriggerInnerDeadzone,
            appSettings.LeftTriggerOuterDeadzone,
            appSettings.RightTriggerInnerDeadzone,
            appSettings.RightTriggerOuterDeadzone,
            dzShape);
        _gamepadService = new GamepadService(reader);
        
        _keyboardCaptureService = keyboardCaptureService ?? new KeyboardCaptureService();
        _elevationHandler = elevationHandler ?? new ElevationHandlerService(_processTargetService);
        _appStatusMonitor = appStatusMonitor ?? new AppStatusMonitor(_processTargetService, _elevationHandler, initialGracePeriodMs: appSettings.FocusGracePeriodMs);

        var engine = mappingEngine ?? CreateMappingEngine();
        _mappingManager = new MappingManager(engine, _profileService);
        _mappingManager.MappingsChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MappingCount));
            OnPropertyChanged(nameof(Mappings));
        };
        if (_mappingManager is INotifyPropertyChanged mappingNotify)
            mappingNotify.PropertyChanged += OnMappingManagerPropertyChanged;
        _mappingManager.OnInputProcessed += (frame, result) => GamepadMonitorPanel.RecordInputFrameSnapshot(frame, result, 
            reader is GamepadReader gr1 ? gr1.LeftThumbstickDeadzone : 0, 
            reader is GamepadReader gr2 ? gr2.RightThumbstickDeadzone : 0);

        InitializeUiSettings(appSettings);

        InitializeChildViewModels(initialLeftDeadzone, initialRightDeadzone, appSettings);

        _profileService.ProfilesLoaded += (_, _) => OnPropertyChanged(nameof(AvailableTemplates));
        _appStatusMonitor.StatusChanged += (_, args) => _uiOrchestrator.UpdateStatus(args.State, args.StatusText);
        _gamepadService.OnInputFrame += frame => _mappingManager.ProcessInputFrame(frame, _appStatusMonitor.EvaluateNow());

        _uiOrchestrator.PropertyChanged += (s, e) => {
            OnPropertyChanged(e.PropertyName);
        };
        _profileOrchestrator.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        _settingsOrchestrator.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

        _profileOrchestrator.LoadSelectedTemplate();
        StartGamepad();
        RefreshRightPanelSurface();
        _dispatcher.BeginInvoke(ScheduleInitialToasts, DispatcherPriority.Loaded);
    }

    public ObservableCollection<MappingEntry> Mappings => _mappingManager.Mappings;
    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _mappingManager.KeyboardActions;
    public ObservableCollection<RadialMenuDefinition> RadialMenus => _mappingManager.RadialMenus;
    public int MappingCount => _mappingManager.MappingCount;

    public MappingEntry? SelectedMapping
    {
        get => _mappingManager.SelectedMapping;
        set => _mappingManager.SelectedMapping = value;
    }

    public bool IsGamepadRunning => _gamepadService.IsRunning;

    [ObservableProperty]
    private int profileListTabIndex;

    [ObservableProperty]
    private ProfileRightPanelSurface rightPanelSurface;

    public ProfileTemplatePanelViewModel ProfileTemplatePanel { get; private set; } = null!;
    public NewBindingPanelViewModel NewBindingPanel { get; private set; } = null!;
    public MappingEditorViewModel MappingEditorPanel { get; private set; } = null!;
    public ProfileCatalogPanelViewModel CatalogPanel { get; private set; } = null!;
    public CommunityCatalogViewModel CommunityCatalogPanel { get; private set; } = null!;
    public GamepadMonitorViewModel GamepadMonitorPanel { get; private set; } = null!;
    public ProcessTargetPanelViewModel ProcessTargetPanel { get; private set; } = null!;

    [ObservableProperty]
    private ProcessInfo? selectedTargetProcess;

    [ObservableProperty]
    private bool isProcessTargetingEnabled;

    public string TargetStatusText => _uiOrchestrator.TargetStatusText;
    public AppTargetingState TargetState => _uiOrchestrator.TargetState;

    private void OnMappingManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedMapping))
            OnPropertyChanged(nameof(SelectedMapping));
    }

    private void OnTemplateLoaded(GameProfileTemplate? template)
    {
        if (template is null) return;
        _mappingManager.LoadTemplate(template);
        ApplyDeclaredProcessTarget();
        UpdateTemplateToggleDisplayNames();
        CatalogPanel.ResetSelection();
        RefreshRightPanelSurface();
    }

    private void ApplyDeclaredProcessTarget()
    {
        SelectedTargetProcess = string.IsNullOrWhiteSpace(TemplateTargetProcessName) 
            ? null 
            : _processTargetService.CreateTargetFromDeclaredProcessName(TemplateTargetProcessName);
    }

    private void SyncAppStatusMonitor() => _appStatusMonitor.UpdateTarget(SelectedTargetProcess, IsProcessTargetingEnabled);

    partial void OnSelectedTargetProcessChanged(ProcessInfo? value)
    {
        if (value is not null) _elevationHandler.CheckAndPromptElevation(value);
        IsProcessTargetingEnabled = value is not null;
        SyncAppStatusMonitor();
    }

    partial void OnIsProcessTargetingEnabledChanged(bool value) => SyncAppStatusMonitor();

    // Settings Properties
    [ObservableProperty] private int focusGracePeriodMsSetting;
    partial void OnFocusGracePeriodMsSettingChanged(int value) => UpdateSetting(s => s.FocusGracePeriodMs = Math.Clamp(value, 0, 5000), s => _appStatusMonitor.UpdateGracePeriod(s.FocusGracePeriodMs));

    [ObservableProperty] private int modifierGraceMsSetting;
    partial void OnModifierGraceMsSettingChanged(int value) => UpdateSetting(s => s.ModifierGraceMs = Math.Clamp(value, 50, 10_000));

    [ObservableProperty] private int leadKeyReleaseSuppressMsSetting;
    partial void OnLeadKeyReleaseSuppressMsSettingChanged(int value) => UpdateSetting(s => s.LeadKeyReleaseSuppressMs = Math.Clamp(value, 50, 10_000));

    [ObservableProperty] private int gamepadPollingIntervalMs;
    partial void OnGamepadPollingIntervalMsChanged(int value) => UpdateSetting(s => s.GamepadPollingIntervalMs = Math.Clamp(value, 5, 30));

    [ObservableProperty] private int radialMenuConfirmModeIndex;
    partial void OnRadialMenuConfirmModeIndexChanged(int value) => UpdateSetting(s => s.RadialMenuConfirmMode = value == 0 ? "returnStickToCenter" : "releaseGuideKey");

    [ObservableProperty] private int radialMenuHudLabelModeIndex;
    partial void OnRadialMenuHudLabelModeIndexChanged(int value) => UpdateSetting(s => s.RadialMenuHudLabelMode = RadialMenuHudLabelModeParser.ToSettingString((RadialMenuHudLabelMode)Math.Clamp(value, 0, 2)));

    [ObservableProperty] private double radialHudScaleSetting;
    partial void OnRadialHudScaleSettingChanged(double value) => UpdateSetting(s => s.RadialHudScale = RadialHudLayout.ClampHudScale(value), s => RadialHudLayout.HudScale = s.RadialHudScale);

    [ObservableProperty] private float defaultAnalogActivationThreshold;
    partial void OnDefaultAnalogActivationThresholdChanged(float value) => UpdateSetting(s => s.DefaultAnalogActivationThreshold = Math.Clamp(value, 0.01f, 1f));

    [ObservableProperty] private float mouseLookSensitivity;
    partial void OnMouseLookSensitivityChanged(float value) => UpdateSetting(s => s.MouseLookSensitivity = Math.Clamp(value, 1f, 100f));

    [ObservableProperty] private float mouseLookSmoothing;
    partial void OnMouseLookSmoothingChanged(float value) => UpdateSetting(s => s.MouseLookSmoothing = Math.Clamp(value, 0f, 1f));

    [ObservableProperty] private float mouseLookSettleMagnitude;
    partial void OnMouseLookSettleMagnitudeChanged(float value) => UpdateSetting(s => s.MouseLookSettleMagnitude = Math.Clamp(value, 0.001f, 0.25f));

    [ObservableProperty] private float mouseLookReboundSuppression;
    partial void OnMouseLookReboundSuppressionChanged(float value) => UpdateSetting(s => s.MouseLookReboundSuppression = Math.Clamp(value, 0f, 1f));

    [ObservableProperty] private float defaultAnalogHysteresisPressExtra;
    partial void OnDefaultAnalogHysteresisPressExtraChanged(float value) =>
        UpdateSetting(s => s.DefaultAnalogHysteresisPressExtra = Math.Clamp(value, 0f, 0.2f));

    [ObservableProperty] private float defaultAnalogHysteresisReleaseExtra;
    partial void OnDefaultAnalogHysteresisReleaseExtraChanged(float value) =>
        UpdateSetting(s => s.DefaultAnalogHysteresisReleaseExtra = Math.Clamp(value, 0f, 0.3f));

    [ObservableProperty] private int thumbstickDeadzoneShapeIndex;
    partial void OnThumbstickDeadzoneShapeIndexChanged(int value)
    {
        var shape = (ThumbstickDeadzoneShape)Math.Clamp(value, 0, 1);
        var setting = ThumbstickDeadzoneShapeParser.ToSettingString(shape);
        if (!string.Equals(setting, _settingsOrchestrator.Settings.ThumbstickDeadzoneShape, StringComparison.OrdinalIgnoreCase))
            UpdateSetting(s => s.ThumbstickDeadzoneShape = setting);
        _gamepadService.SetThumbstickDeadzoneShape(shape);
    }

    [ObservableProperty] private float analogChangeEpsilon;
    partial void OnAnalogChangeEpsilonChanged(float value) => UpdateSetting(s => s.AnalogChangeEpsilon = Math.Clamp(value, 0.001f, 0.1f));

    [ObservableProperty] private int keyboardTapHoldDurationMs;
    partial void OnKeyboardTapHoldDurationMsChanged(int value) => UpdateSetting(s => s.KeyboardTapHoldDurationMs = Math.Clamp(value, 20, 100));

    [ObservableProperty] private int tapInterKeyDelayMs;
    partial void OnTapInterKeyDelayMsChanged(int value) => UpdateSetting(s => s.TapInterKeyDelayMs = Math.Clamp(value, 0, 1000));

    [ObservableProperty] private int textInterCharDelayMs;
    partial void OnTextInterCharDelayMsChanged(int value) => UpdateSetting(s => s.TextInterCharDelayMs = Math.Clamp(value, 0, 1000));

    [ObservableProperty] private bool humanNoiseEnabled;
    partial void OnHumanNoiseEnabledChanged(bool value) =>
        UpdateSetting(s => s.HumanNoiseEnabled = value);

    [ObservableProperty] private float humanNoiseAmplitude;
    partial void OnHumanNoiseAmplitudeChanged(float value) => UpdateSetting(s => s.HumanNoiseAmplitude = Math.Clamp(value, 0f, 1f));

    [ObservableProperty] private float humanNoiseFrequency;
    partial void OnHumanNoiseFrequencyChanged(float value) => UpdateSetting(s => s.HumanNoiseFrequency = Math.Clamp(value, 0f, 1f));

    [ObservableProperty] private float humanNoiseSmoothness;
    partial void OnHumanNoiseSmoothnessChanged(float value) => UpdateSetting(s => s.HumanNoiseSmoothness = Math.Clamp(value, 0f, 1f));

    public ObservableCollection<ComboHudPlacement> ComboHudPlacements { get; } = new(Enum.GetValues<ComboHudPlacement>());

    public ObservableCollection<InputApiOption> AvailableInputApis { get; } = new();
    public ObservableCollection<InputApiOption> AvailableGamepadApis { get; } = new();

    [ObservableProperty] private InputApiOption? selectedInputApi;
    [ObservableProperty] private InputApiOption? selectedGamepadApi;

    private bool _suppressInputApiUiSync;
    private bool _suppressGamepadSourceUiSync;

    partial void OnSelectedInputApiChanged(InputApiOption? value)
    {
        if (_suppressInputApiUiSync || value == null) return;
        var apiId = NormalizeInputEmulationApiId(value.Id);
        if (string.Equals(_settingsOrchestrator.Settings.InputEmulationApi, apiId, StringComparison.OrdinalIgnoreCase)) return;

        UpdateSetting(s => s.InputEmulationApi = apiId);
        RecreateMappingEngineForCurrentInputApi();
    }

    partial void OnSelectedGamepadApiChanged(InputApiOption? value)
    {
        if (_suppressGamepadSourceUiSync || value == null) return;
        UpdateGamepadSource(value.Id);
    }

    private void UpdateGamepadSource(string apiId)
    {
        var prev = _settingsOrchestrator.Settings.GamepadSourceApi;
        if (string.Equals(prev, apiId, StringComparison.OrdinalIgnoreCase)) return;

        UpdateSetting(s => s.GamepadSourceApi = apiId);
        RecreateGamepadReaderForCurrentSource();
    }

    private void SyncInputApiUi(string apiId)
    {
        _suppressInputApiUiSync = true;
        try
        {
            SelectedInputApi = AvailableInputApis.FirstOrDefault(a => string.Equals(a.Id, apiId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _suppressInputApiUiSync = false;
        }
    }

    private void SyncGamepadSourceUi(string apiId)
    {
        _suppressGamepadSourceUiSync = true;
        try
        {
            SelectedGamepadApi = AvailableGamepadApis.FirstOrDefault(a => string.Equals(a.Id, apiId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _suppressGamepadSourceUiSync = false;
        }
    }

    private void RecreateGamepadReaderForCurrentSource()
    {
        var api = _settingsOrchestrator.Settings.GamepadSourceApi;
        IGamepadSource source = api switch
        {
            GamepadSourceApiIds.DualSense => throw new NotImplementedException("DualSense source not yet implemented."),
            _ => new XInputSource(new XInputService())
        };

        var dzShape = ThumbstickDeadzoneShapeParser.Parse(_settingsOrchestrator.Settings.ThumbstickDeadzoneShape);
        var reader = new GamepadReader(
            source,
            GamepadMonitorPanel.LeftThumbstickDeadzone,
            GamepadMonitorPanel.RightThumbstickDeadzone,
            GamepadMonitorPanel.LeftTriggerInnerDeadzone,
            GamepadMonitorPanel.LeftTriggerOuterDeadzone,
            GamepadMonitorPanel.RightTriggerInnerDeadzone,
            GamepadMonitorPanel.RightTriggerOuterDeadzone,
            dzShape);

        _gamepadService.ReplaceReader(reader);
        OnPropertyChanged(nameof(IsGamepadRunning));
    }

    private void UpdateSetting(Action<AppSettings> update, Action<AppSettings>? after = null)
    {
        update(_settingsOrchestrator.Settings);
        _settingsOrchestrator.SaveSettings();
        after?.Invoke(_settingsOrchestrator.Settings);
    }

    public IKeyboardCaptureService KeyboardCaptureService => _keyboardCaptureService;
    public IProfileService GetProfileService() => _profileService;
    public ObservableCollection<string> AvailableGamepadButtons { get; } = new(GamepadChordSegmentCatalog.AllSegmentNames);
    public ObservableCollection<TriggerMoment> AvailableTriggerModes { get; } = new(Enum.GetValues<TriggerMoment>());

    public void RefreshTemplates(string? preferredProfileId = null) => _profileOrchestrator.RefreshTemplates(preferredProfileId);
    public void ReloadSelectedTemplate() => _profileOrchestrator.LoadSelectedTemplate();
    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null) => _keyboardCaptureService.TryCaptureKeyboardKey(key, systemKey);
    public void CancelKeyboardKeyRecording() => _keyboardCaptureService.CancelCapture();

    public string CurrentTemplateProfileId { get => _profileOrchestrator.CurrentTemplateProfileId; set => _profileOrchestrator.CurrentTemplateProfileId = value; }
    public string CurrentTemplateTemplateGroupId { get => _profileOrchestrator.CurrentTemplateTemplateGroupId; set => _profileOrchestrator.CurrentTemplateTemplateGroupId = value; }
    public string CurrentTemplateAuthor { get => _profileOrchestrator.CurrentTemplateAuthor; set => _profileOrchestrator.CurrentTemplateAuthor = value; }
    public string CurrentTemplateCatalogFolder { get => _profileOrchestrator.CurrentTemplateCatalogFolder; set => _profileOrchestrator.CurrentTemplateCatalogFolder = value; }
    public IReadOnlyList<string>? ComboLeadButtonsPersist => _profileOrchestrator.ComboLeadButtonsPersist;

    [RelayCommand] private void StartGamepad() { _gamepadService.Start(); OnPropertyChanged(nameof(IsGamepadRunning)); GamepadMonitorPanel.IsGamepadRunning = true; }
    [RelayCommand] private void StopGamepad() { _gamepadService.Stop(); _mappingManager.ForceReleaseOutputs(); OnPropertyChanged(nameof(IsGamepadRunning)); GamepadMonitorPanel.IsGamepadRunning = false; }

    public void Dispose()
    {
        if (_mappingManager is INotifyPropertyChanged mappingNotify)
            mappingNotify.PropertyChanged -= OnMappingManagerPropertyChanged;
        GamepadMonitorPanel.PropertyChanged -= OnGamepadMonitorPanelSettingsChanged;
        GamepadMonitorPanel.Dispose();
        _uiOrchestrator.Dispose();
        _gamepadService.Dispose();
        _mappingManager.Dispose();
        _radialMenuHud.Dispose();
        _appStatusMonitor.Dispose();
        _toastHost.Dispose();
    }

    public ObservableCollection<TemplateOption> AvailableTemplates => _profileOrchestrator.AvailableTemplates;
    public TemplateOption? SelectedTemplate { get => _profileOrchestrator.SelectedTemplate; set => _profileOrchestrator.SelectedTemplate = value; }
    public string CurrentTemplateDisplayName { get => _profileOrchestrator.CurrentTemplateDisplayName; set => _profileOrchestrator.CurrentTemplateDisplayName = value; }
    public string TemplateTargetProcessName
    {
        get => _profileOrchestrator.TemplateTargetProcessName;
        set
        {
            if (_profileOrchestrator.TemplateTargetProcessName == value) return;
            _profileOrchestrator.TemplateTargetProcessName = value;
            _processNameDebouncer.Debounce(ApplyDeclaredProcessTarget);
            OnPropertyChanged();
        }
    }
    
    public ObservableCollection<UiLanguageOption> AvailableUiLanguages => _settingsOrchestrator.AvailableUiLanguages;
    public UiLanguageOption? SelectedUiLanguage { get => _settingsOrchestrator.SelectedUiLanguage; set => _settingsOrchestrator.SelectedUiLanguage = value; }

    private void OnComboHud(ComboHudContent? content)
    {
        void Apply()
        {
            if (!GamepadMonitorPanel.IsHudEnabled)
            {
                _uiOrchestrator.HideAllHuds();
                return;
            }

            _uiOrchestrator.ShowComboHud(content, (byte)GamepadMonitorPanel.ComboHudPanelAlpha, GamepadMonitorPanel.ComboHudShadowOpacity, ComboHudPlacementSetting.ToString());
        }

        if (_dispatcher.CheckAccess())
            Apply();
        else
            _dispatcher.BeginInvoke(Apply, DispatcherPriority.Normal);
    }

    private void ShowTemplateSwitchHud(string profileDisplayName)
    {
        if (!GamepadMonitorPanel.IsHudEnabled) return;
        _uiOrchestrator.ShowTemplateSwitchHud(profileDisplayName, GamepadMonitorPanel.TemplateSwitchHudSeconds, (byte)GamepadMonitorPanel.ComboHudPanelAlpha, GamepadMonitorPanel.ComboHudShadowOpacity, ComboHudPlacementSetting.ToString(), () => { });
    }

    private IMappingEngine CreateMappingEngine()
    {
        if (_keyboardEmulatorOverride is { } kbdOverride && _mouseEmulatorOverride is { } msOverride)
            return NewMappingEngine(kbdOverride, msOverride);

        var (kbd, mouse) = CreateEmulatorPair();
        return NewMappingEngine(kbd, mouse);
    }

    private (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreateEmulatorPair() =>
        _inputEmulationStackFactory.CreatePair(
            _settingsOrchestrator.Settings.InputEmulationApi,
            () => HumanInputNoiseParameters.From(_settingsOrchestrator.Settings));

    private IMappingEngine NewMappingEngine(IKeyboardEmulator keyboard, IMouseEmulator mouse) =>
        new MappingEngine(
            keyboard,
            mouse,
            () => _appStatusMonitor.CanSendOutput,
            _uiSync,
            v => GamepadMonitorPanel.LastMappedOutput = v,
            v => GamepadMonitorPanel.LastMappingStatus = v,
            OnComboHud,
            _profileService.ModifierGraceMs,
            _profileService.LeadKeyReleaseSuppressMs,
            pid => _dispatcher.BeginInvoke(() => _profileOrchestrator.RequestTemplateSwitch(pid)),
            profileService: _profileService,
            setComboHudGateHint: s => _dispatcher.BeginInvoke(() => GamepadMonitorPanel.ComboHudGateHint = s ?? string.Empty),
            comboHudGateMessageFactory: _settingsOrchestrator.GetComboHudGateMessageFactory(),
            radialMenuHud: _radialMenuHud,
            getRadialMenuStickEngagementThreshold: () => DefaultAnalogActivationThreshold,
            getRadialMenuConfirmMode: () => RadialMenuConfirmModeIndex == 0 ? RadialMenuConfirmMode.ReturnStickToCenter : RadialMenuConfirmMode.ReleaseGuideKey,
            ownsRadialMenuHud: false,
            getMouseLookSensitivity: () => _settingsOrchestrator.Settings.MouseLookSensitivity,
            getMouseLookSmoothing: () => _settingsOrchestrator.Settings.MouseLookSmoothing,
            getMouseLookSettleMagnitude: () => _settingsOrchestrator.Settings.MouseLookSettleMagnitude,
            getMouseLookReboundSuppression: () => _settingsOrchestrator.Settings.MouseLookReboundSuppression,
            getGamepadPollingIntervalMs: () => _settingsOrchestrator.Settings.GamepadPollingIntervalMs,
            getAnalogChangeEpsilon: () => _settingsOrchestrator.Settings.AnalogChangeEpsilon,
            getAnalogHysteresisPressExtra: () => _settingsOrchestrator.Settings.DefaultAnalogHysteresisPressExtra,
            getAnalogHysteresisReleaseExtra: () => _settingsOrchestrator.Settings.DefaultAnalogHysteresisReleaseExtra,
            getKeyboardTapHoldDurationMs: () => _settingsOrchestrator.Settings.KeyboardTapHoldDurationMs);

    private void RecreateMappingEngineForCurrentInputApi()
    {
        if (_keyboardEmulatorOverride is not null && _mouseEmulatorOverride is not null)
            return;

        var (kbd, mouse) = CreateEmulatorPair();
        _mappingManager.ReplaceEngine(NewMappingEngine(kbd, mouse), ComboLeadButtonsPersist);
    }

    private static string NormalizeInputEmulationApiId(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? InputEmulationApiIds.Win32 : raw.Trim();

    // Helper methods for UI refresh
    internal void RefreshRightPanelSurface()
    {
        var showMapping = ProfileListTabIndex == 0 && (SelectedMapping is not null || MappingEditorPanel.IsCreatingNewMapping);
        var showKeyboard = ProfileListTabIndex == 1 && CatalogPanel.SelectedKeyboardAction is not null;
        var showRadial = ProfileListTabIndex == 2 && CatalogPanel.SelectedRadialMenu is not null;

        RightPanelSurface = showMapping ? ProfileRightPanelSurface.Mapping : showKeyboard ? ProfileRightPanelSurface.KeyboardAction : showRadial ? ProfileRightPanelSurface.RadialMenu : ProfileRightPanelSurface.None;
    }

    private void UpdateTemplateToggleDisplayNames()
    {
        if (Mappings is null) return;
        foreach (var mapping in Mappings)
        {
            if (mapping.TemplateToggle is null) { mapping.TemplateToggleDisplayName = string.Empty; continue; }
            var targetId = mapping.TemplateToggle.AlternateProfileId?.Trim() ?? string.Empty;
            if (targetId.Length > 0 && _profileService.TryResolveTemplateLocation(targetId, out var loc))
                mapping.TemplateToggleDisplayName = AvailableTemplates.FirstOrDefault(t => t.MatchesLocation(loc))?.DisplayName ?? string.Empty;
        }
    }

    private static float ResolveStickDeadzone(float specific, float shared) => specific > 0f ? Math.Clamp(specific, 0f, 0.9f) : Math.Clamp(shared, 0f, 0.9f);

    [ObservableProperty] private ComboHudPlacement comboHudPlacementSetting;
    partial void OnComboHudPlacementSettingChanged(ComboHudPlacement value) => UpdateSetting(s => s.ComboHudPlacement = value.ToString());

    private void InitializeUiSettings(AppSettings appSettings)
    {
        FocusGracePeriodMsSetting = appSettings.FocusGracePeriodMs;
        ModifierGraceMsSetting = appSettings.ModifierGraceMs;
        LeadKeyReleaseSuppressMsSetting = appSettings.LeadKeyReleaseSuppressMs;
        GamepadPollingIntervalMs = appSettings.GamepadPollingIntervalMs;
        RadialMenuConfirmModeIndex = string.Equals(appSettings.RadialMenuConfirmMode, "returnStickToCenter", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        RadialMenuHudLabelModeIndex = (int)RadialMenuHudLabelModeParser.Parse(appSettings.RadialMenuHudLabelMode);
        RadialHudScaleSetting = appSettings.RadialHudScale;
        DefaultAnalogActivationThreshold = appSettings.DefaultAnalogActivationThreshold;
        MouseLookSensitivity = appSettings.MouseLookSensitivity;
        MouseLookSmoothing = appSettings.MouseLookSmoothing;
        MouseLookSettleMagnitude = appSettings.MouseLookSettleMagnitude;
        MouseLookReboundSuppression = appSettings.MouseLookReboundSuppression;
        DefaultAnalogHysteresisPressExtra = appSettings.DefaultAnalogHysteresisPressExtra;
        DefaultAnalogHysteresisReleaseExtra = appSettings.DefaultAnalogHysteresisReleaseExtra;
        ThumbstickDeadzoneShapeIndex = ThumbstickDeadzoneShapeParser.Parse(appSettings.ThumbstickDeadzoneShape) == ThumbstickDeadzoneShape.Radial ? 1 : 0;
        AnalogChangeEpsilon = appSettings.AnalogChangeEpsilon;
        KeyboardTapHoldDurationMs = appSettings.KeyboardTapHoldDurationMs;
        TapInterKeyDelayMs = appSettings.TapInterKeyDelayMs;
        TextInterCharDelayMs = appSettings.TextInterCharDelayMs;
        HumanNoiseEnabled = appSettings.HumanNoiseEnabled;
        HumanNoiseAmplitude = appSettings.HumanNoiseAmplitude;
        HumanNoiseFrequency = appSettings.HumanNoiseFrequency;
        HumanNoiseSmoothness = appSettings.HumanNoiseSmoothness;
        ComboHudPlacementSetting = Enum.TryParse<ComboHudPlacement>(appSettings.ComboHudPlacement, out var p) ? p : ComboHudPlacement.BottomRight;

        AvailableInputApis.Clear();
        AvailableInputApis.Add(new InputApiOption(InputEmulationApiIds.Win32, _settingsOrchestrator.Localize("InputApiWin32Label")));
        AvailableInputApis.Add(new InputApiOption(InputEmulationApiIds.InputInjection, _settingsOrchestrator.Localize("InputApiInputInjectionLabel")));

        AvailableGamepadApis.Clear();
        AvailableGamepadApis.Add(new InputApiOption(GamepadSourceApiIds.XInput, _settingsOrchestrator.Localize("GamepadSourceXInputLabel")));
        AvailableGamepadApis.Add(new InputApiOption(GamepadSourceApiIds.DualSense, _settingsOrchestrator.Localize("GamepadSourceDualSenseLabel")));

        SyncInputApiUi(NormalizeInputEmulationApiId(appSettings.InputEmulationApi));
        SyncGamepadSourceUi(appSettings.GamepadSourceApi ?? GamepadSourceApiIds.XInput);
    }

    private void InitializeChildViewModels(float leftDz, float rightDz, AppSettings s)
    {
        ProfileTemplatePanel = new ProfileTemplatePanelViewModel(this);
        NewBindingPanel = new NewBindingPanelViewModel(this);
        MappingEditorPanel = new MappingEditorViewModel(this);
        CatalogPanel = new ProfileCatalogPanelViewModel(this);
        CommunityCatalogPanel = new CommunityCatalogViewModel(this, _communityService);
        GamepadMonitorPanel = new GamepadMonitorViewModel(StopGamepadCommand, StartGamepadCommand, b => _uiOrchestrator.HideAllHuds(), leftDz, rightDz, (l, r) => _gamepadService.SetThumbstickDeadzones(l, r), s.LeftTriggerInnerDeadzone, s.LeftTriggerOuterDeadzone, s.RightTriggerInnerDeadzone, s.RightTriggerOuterDeadzone, (li, lo, ri, ro) => _gamepadService.SetTriggerDeadzones(li, lo, ri, ro), s.ComboHudPanelAlpha, s.ComboHudShadowOpacity, (a, o) => _uiOrchestrator.ApplyHudVisuals((byte)a, o), s.TemplateSwitchHudSeconds, _ => { }, _dispatcher);
        ApplyGamepadMonitorInitialUiState(s);
        GamepadMonitorPanel.PropertyChanged += OnGamepadMonitorPanelSettingsChanged;
        ProcessTargetPanel = new ProcessTargetPanelViewModel(this);
    }

    private void ApplyGamepadMonitorInitialUiState(AppSettings s)
    {
        _suppressGamepadMonitorSettingsPersistence = true;
        try
        {
            GamepadMonitorPanel.MonitorPanelWidth = GamepadMonitorViewModel.ClampMonitorWidth(s.GamepadMonitorPanelWidth);
            GamepadMonitorPanel.IsMonitorExpanderExpanded = s.GamepadMonitorVisible;
        }
        finally
        {
            _suppressGamepadMonitorSettingsPersistence = false;
        }
    }

    private void OnGamepadMonitorPanelSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressGamepadMonitorSettingsPersistence) return;
        if (e.PropertyName == nameof(GamepadMonitorViewModel.MonitorPanelWidth))
            UpdateSetting(s => s.GamepadMonitorPanelWidth = GamepadMonitorViewModel.ClampMonitorWidth(GamepadMonitorPanel.MonitorPanelWidth));
        else if (e.PropertyName == nameof(GamepadMonitorViewModel.IsMonitorExpanderExpanded))
            UpdateSetting(s => s.GamepadMonitorVisible = GamepadMonitorPanel.IsMonitorExpanderExpanded);
    }

    private void ScheduleInitialToasts()
    {
        if (App.LaunchUpdateSuccessArgs is not null)
            _uiOrchestrator.ShowToast(_settingsOrchestrator.Localize("UpdateSuccessToastTitle"), _settingsOrchestrator.FormatUpdateSuccessMessage(App.LaunchUpdateSuccessArgs.Value.ReleaseTag));
    }
}

