using System;
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
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services;
using Gamepad_Mapping.Utils;
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

    private readonly ObservableCollection<KeyboardActionDefinition> _keyboardActions = new();
    private readonly ObservableCollection<RadialMenuDefinition> _radialMenus = new();

    /// <summary>Last loaded template's effective group id; used to carry <c>targetProcessName</c> across related profiles.</summary>
    private string? _lastLoadedTemplateGroupIdForTargetInherit;
    private ComboHudWindow? _comboHudWindow;
    private TemplateSwitchHudWindow? _templateSwitchHudWindow;
    private readonly AppSettings _appSettings;
    private readonly ISettingsService _settingsService;
    private DispatcherTimer? _templateSwitchHudTimer;
    private bool _isTemplateSwitchHudActive;
    private bool _isInitializingUiLanguageSelection;

    private readonly ICommunityTemplateService _communityService;
    private readonly IUpdateService _updateService;
    private readonly ILocalFileService _localFileService;
    private readonly IUpdateInstallerService _updateInstallerService;
    private readonly IUpdateNotificationService _updateNotificationService;
    private readonly IAppToastService _appToastService;
    private readonly AppToastViewModel _toastHost;

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
        IAppToastService? appToastService = null)
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _settingsService = settingsService ?? new SettingsService();
        _appSettings = _settingsService.LoadSettings();
        _profileService = profileService ?? new ProfileService(settingsService: _settingsService, appSettings: _appSettings);
        _localFileService = localFileService ?? new LocalFileService();
        var sharedGitHubContentService = gitHubContentService ?? new GitHubContentService();
        var resolvedUpdateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();
        _communityService = communityService ?? new CommunityTemplateService(_profileService, sharedGitHubContentService, _localFileService);
        _updateService = updateService ?? new UpdateService(sharedGitHubContentService, _settingsService, _appSettings, resolvedUpdateVersionCacheService);
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();
        _updateNotificationService = updateNotificationService ?? new UpdateNotificationService();
        _appToastService = appToastService ?? new AppToastService();
        _toastHost = new AppToastViewModel(_appToastService);
        var resolvedTrustedUtcTimeService = trustedUtcTimeService ?? new TrustedUtcTimeService();
        var resolvedUpdateQuotaPolicyProvider = updateQuotaPolicyProvider ?? new StaticUpdateQuotaPolicyProvider();
        var resolvedUpdateQuotaService = updateQuotaService ?? new UpdateQuotaService(resolvedUpdateQuotaPolicyProvider, resolvedTrustedUtcTimeService);

        UpdatePanel = new UpdateViewModel(
            _updateService,
            _settingsService,
            _appSettings,
            _localFileService,
            _updateInstallerService,
            resolvedUpdateQuotaService,
            resolvedUpdateVersionCacheService);
        ModifierGraceMsSetting = _appSettings.ModifierGraceMs;
        LeadKeyReleaseSuppressMsSetting = _appSettings.LeadKeyReleaseSuppressMs;
        GamepadPollingIntervalMs = _appSettings.GamepadPollingIntervalMs;
        RadialMenuConfirmModeIndex =
            string.Equals(_appSettings.RadialMenuConfirmMode, "returnStickToCenter", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1;
        RadialMenuHudLabelModeIndex = (int)RadialMenuHudLabelModeParser.Parse(_appSettings.RadialMenuHudLabelMode);
        RadialHudScaleSetting = RadialHudLayout.ClampHudScale(_appSettings.RadialHudScale);
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
        AvailableGamepadButtons = new ObservableCollection<string>(GamepadChordSegmentCatalog.AllSegmentNames);
        AvailableTriggerModes = new ObservableCollection<TriggerMoment>(Enum.GetValues<TriggerMoment>());
        Mappings.CollectionChanged += (_, _) =>
        {
            MappingCount = Mappings.Count;
            _mappingsSnapshot = Mappings.ToList();
        };

        ProfileTemplatePanel = new ProfileTemplatePanelViewModel(this);
        NewBindingPanel = new NewBindingPanelViewModel(this);
        MappingEditorPanel = new MappingEditorViewModel(this);
        CatalogPanel = new ProfileCatalogPanelViewModel(this);
        CommunityCatalogPanel = new CommunityCatalogViewModel(this, _communityService);
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
            initialTemplateSwitchHudSeconds: Math.Clamp(_appSettings.TemplateSwitchHudSeconds, 0.5, 5.0),
            templateSwitchHudChanged: OnTemplateSwitchHudSecondsChanged,
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
            profileService: _profileService,
            setComboHudGateHint: s => DispatchToUi(() => GamepadMonitorPanel.ComboHudGateHint = s ?? string.Empty),
            comboHudGateMessageFactory: comboHudGateMessageFactory,
            isComboHudPresentationSuppressed: () => _isTemplateSwitchHudActive,
            radialMenuHud: new RadialMenuHudPresenter(
                () => (RadialMenuHudLabelMode)RadialMenuHudLabelModeIndex,
                () => Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220)),
            getRadialMenuStickEngagementThreshold: () => DefaultAnalogActivationThreshold,
            getRadialMenuConfirmMode: () => RadialMenuConfirmModeIndex == 0
                ? RadialMenuConfirmMode.ReturnStickToCenter
                : RadialMenuConfirmMode.ReleaseGuideKey);
        _keyboardActions.CollectionChanged += (_, _) => RefreshRadialDefinitionsInEngine();
        _radialMenus.CollectionChanged += OnRadialMenusCollectionChanged;
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
        RefreshRightPanelSurface();
        _dispatcher.BeginInvoke(ScheduleInitialToasts, DispatcherPriority.Loaded);
    }

    partial void OnProfileListTabIndexChanged(int value) => RefreshRightPanelSurface();

    internal void RefreshRightPanelSurface()
    {
        var showMapping = ProfileListTabIndex == 0 &&
            (SelectedMapping is not null || MappingEditorPanel.IsCreatingNewMapping);
        var showKeyboard = ProfileListTabIndex == 1 && CatalogPanel.SelectedKeyboardAction is not null;
        var showRadial = ProfileListTabIndex == 2 && CatalogPanel.SelectedRadialMenu is not null;

        RightPanelSurface = showMapping
            ? ProfileRightPanelSurface.Mapping
            : showKeyboard
                ? ProfileRightPanelSurface.KeyboardAction
                : showRadial
                    ? ProfileRightPanelSurface.RadialMenu
                    : ProfileRightPanelSurface.None;
    }

    [ObservableProperty]
    private ObservableCollection<TemplateOption> availableTemplates;

    [ObservableProperty]
    private TemplateOption? selectedTemplate;

    partial void OnSelectedTemplateChanged(TemplateOption? value)
    {
        try
        {
            App.Logger.Info($"Switching to template: {value?.DisplayName} ({value?.StorageKey})");
            LoadSelectedTemplate();
            _profileService.PersistLastSelectedTemplateProfileId(value?.StorageKey);
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
    private string currentTemplateProfileId = string.Empty;

    [ObservableProperty]
    private string currentTemplateTemplateGroupId = string.Empty;

    [ObservableProperty]
    private string currentTemplateAuthor = string.Empty;

    [ObservableProperty]
    private string currentTemplateCatalogFolder = string.Empty;

    [ObservableProperty]
    private int mappingCount;

    [ObservableProperty]
    private bool isGamepadRunning;

    /// <summary>Left workspace: 0 mappings, 1 keyboard actions, 2 radial menus.</summary>
    [ObservableProperty]
    private int profileListTabIndex;

    [ObservableProperty]
    private ProfileRightPanelSurface rightPanelSurface;

    public ProfileTemplatePanelViewModel ProfileTemplatePanel { get; }

    public NewBindingPanelViewModel NewBindingPanel { get; }

    public MappingEditorViewModel MappingEditorPanel { get; }

    public ProfileCatalogPanelViewModel CatalogPanel { get; }

    public CommunityCatalogViewModel CommunityCatalogPanel { get; }

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
    private int radialMenuConfirmModeIndex;

    partial void OnRadialMenuConfirmModeIndexChanged(int value)
    {
        var clamped = value < 0 ? 0 : (value > 1 ? 1 : value);
        if (clamped != value)
            radialMenuConfirmModeIndex = clamped;
        _appSettings.RadialMenuConfirmMode = clamped == 0 ? "returnStickToCenter" : "releaseGuideKey";
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private int radialMenuHudLabelModeIndex;

    partial void OnRadialMenuHudLabelModeIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
            radialMenuHudLabelModeIndex = clamped;
        _appSettings.RadialMenuHudLabelMode =
            RadialMenuHudLabelModeParser.ToSettingString((RadialMenuHudLabelMode)clamped);
        _settingsService.SaveSettings(_appSettings);
    }

    [ObservableProperty]
    private double radialHudScaleSetting;

    partial void OnRadialHudScaleSettingChanged(double value)
    {
        var clamped = RadialHudLayout.ClampHudScale(value);
        if (Math.Abs(clamped - value) > 1e-6)
            radialHudScaleSetting = clamped;
        RadialHudLayout.HudScale = clamped;
        _appSettings.RadialHudScale = clamped;
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
        if (id.Length == 0 || !_profileService.TryResolveTemplateLocation(id, out var loc))
            return;

        var opt = _profileService.AvailableTemplates.FirstOrDefault(t => t.MatchesLocation(loc));
        if (opt is null)
            return;

        if (SelectedTemplate?.MatchesLocation(loc) == true)
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

    private void ScheduleInitialToasts()
    {
#if DEBUG
        if (TryShowDebugCornerToast())
            return;
#endif

        var launchArgs = App.LaunchUpdateSuccessArgs;
        if (launchArgs is not null)
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("UpdateSuccessToastTitle"),
                Message = FormatUpdateSuccessMessage(launchArgs.Value.ReleaseTag),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeSuccess(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                InvokeOnClosedWhenExitingApplication = true
            });
            // Clear the argument so it doesn't show again on subsequent launches from e.g. a shortcut
            App.LaunchUpdateSuccessArgs = null;
            return;
        }

        var pendingUpdate = _updateNotificationService.TryGetPendingSuccessToast();
        if (pendingUpdate is not null)
        {
            var p = pendingUpdate.Value;
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("UpdateSuccessToastTitle"),
                Message = FormatUpdateSuccessMessage(p.ReleaseTag),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeSuccess(p.UpdatedAtUnixSeconds),
                InvokeOnClosedWhenExitingApplication = true
            });
            return;
        }

        var pendingFailure = _updateNotificationService.TryGetPendingFailureToast();
        if (pendingFailure is not null)
        {
            var p = pendingFailure.Value;
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("UpdateFailedToastTitle"),
                Message = p.ErrorMessage ?? Localize("UpdateInstallLaunchFailed"),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeFailure(),
                InvokeOnClosedWhenExitingApplication = true
            });
            return;
        }

        if (!_appSettings.HasSeenWelcomeToast)
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("WelcomeToastTitle"),
                Message = Localize("WelcomeToastMessage"),
                AutoHideSeconds = null,
                OnClosed = () =>
                {
                    _appSettings.HasSeenWelcomeToast = true;
                    _settingsService.SaveSettings(_appSettings);
                },
                InvokeOnClosedWhenExitingApplication = false
            });
        }
    }

#if DEBUG
    private bool TryShowDebugCornerToast()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => string.Equals(a, "--debug-toast", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = "Debug: corner toast",
                Message = "Launched with --debug-toast. Dismiss or wait for auto-hide; no update or welcome flow runs.",
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        if (args.Any(a => string.Equals(a, "--debug-update-success", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("UpdateSuccessToastTitle"),
                Message = FormatUpdateSuccessMessage("vDebugSuccess"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        if (args.Any(a => string.Equals(a, "--debug-update-failed", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = Localize("UpdateFailedToastTitle"),
                Message = Localize("UpdateFailedToastMessage"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        return false;
    }
#endif

    private static string Localize(string key)
    {
        if (Application.Current?.Resources["Loc"] is TranslationService loc)
            return loc[key];
        return key;
    }

    private static string FormatUpdateSuccessMessage(string releaseTag)
    {
        if (Application.Current?.Resources["Loc"] is not TranslationService loc)
            return releaseTag;

        return string.Format(loc["UpdateSuccessToastMessage"], releaseTag);
    }

    public void Dispose()
    {
        try
        {
            _gamepadReader.Stop();
            _appToastService.NotifyApplicationExiting();
            _toastHost.Dispose();

            if (_templateSwitchHudTimer is not null)
            {
                _templateSwitchHudTimer.Stop();
                _templateSwitchHudTimer = null;
            }
            if (_comboHudWindow is not null)
            {
                _comboHudWindow.Close();
                _comboHudWindow = null;
            }
            if (_templateSwitchHudWindow is not null)
            {
                _templateSwitchHudWindow.Close();
                _templateSwitchHudWindow = null;
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
        RefreshRightPanelSurface();
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

        var seconds = Math.Clamp(GamepadMonitorPanel.TemplateSwitchHudSeconds, 0.5, 5.0);

        _templateSwitchHudTimer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };

        _templateSwitchHudTimer.Tick += (_, _) =>
        {
            _templateSwitchHudTimer?.Stop();
            _templateSwitchHudTimer = null;
            _isTemplateSwitchHudActive = false;
            _mappingEngine.InvalidateComboHudPresentation();
            _mappingEngine.RefreshComboHud();

            _templateSwitchHudWindow?.HideHud();
        };

        var a = (byte)Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220);
        var o = Math.Clamp(GamepadMonitorPanel.ComboHudShadowOpacity, 0.08, 0.60);

        var title = "Profile switched";
        var line = new ComboHudLine($"→ {profileDisplayName}", null);
        var content = new ComboHudContent(title, new[] { line });

        // Fade out the combo HUD while fading in the template switch HUD.
        _comboHudWindow?.HideHud();

        _templateSwitchHudWindow ??= new TemplateSwitchHudWindow();
        _templateSwitchHudWindow.ShowHud(content, a, o, ComboHudPlacementSetting);

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

    private void OnTemplateSwitchHudSecondsChanged(double seconds)
    {
        var clamped = Math.Clamp(seconds, 0.5, 5.0);
        _appSettings.TemplateSwitchHudSeconds = clamped;
        _settingsService.SaveSettings(_appSettings);
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
                _templateSwitchHudWindow?.HideHud();
            });
    }

    private void LoadSelectedTemplate()
    {
        if (SelectedTemplate is null)
            return;

        var template = _profileService.LoadSelectedTemplate(SelectedTemplate);
        if (template is null)
            return;

        CurrentTemplateDisplayName = template.DisplayName;
        CurrentTemplateProfileId = template.ProfileId;
        CurrentTemplateTemplateGroupId = template.TemplateGroupId ?? string.Empty;
        CurrentTemplateAuthor = template.Author ?? string.Empty;
        CurrentTemplateCatalogFolder = template.TemplateCatalogFolder ?? string.Empty;

        _comboLeadButtonsPersist = template.ComboLeadButtons?.ToList();
        foreach (var rm in _radialMenus.ToList())
            rm.Items.CollectionChanged -= OnRadialMenuItemsCollectionChanged;
        _keyboardActions.Clear();
        _radialMenus.Clear();
        foreach (var a in template.KeyboardActions ?? [])
            _keyboardActions.Add(a);
        foreach (var rm in template.RadialMenus ?? [])
            _radialMenus.Add(rm);
        
        if (_mappingEngine != null)
        {
            _mappingEngine.SetComboLeadButtonsFromTemplate(template.ComboLeadButtons);
            RefreshRadialDefinitionsInEngine();
        }

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
                 && ProfileService.ProfilesLikelyShareGameExecutable(_lastLoadedTemplateGroupIdForTargetInherit, template.EffectiveTemplateGroupId))
        {
            TemplateTargetProcessName = uiBefore;
            template.TargetProcessName = uiBefore;
            _profileService.SaveTemplate(template);
        }
        else
            TemplateTargetProcessName = string.Empty;

        _lastLoadedTemplateGroupIdForTargetInherit = template.EffectiveTemplateGroupId;

        ApplyDeclaredProcessTarget();
        UpdateTemplateToggleDisplayNames();

        CatalogPanel.SelectedKeyboardAction = null;
        CatalogPanel.SelectedRadialMenu = null;
        CatalogPanel.SelectedRadialSlot = null;
        RefreshRightPanelSurface();
    }

    /// <summary>Combo lead names from the loaded template; written back unchanged on Save profile.</summary>
    public IReadOnlyList<string>? ComboLeadButtonsPersist => _comboLeadButtonsPersist;

    /// <summary>Keyboard action catalog for the current profile (saved with the template).</summary>
    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _keyboardActions;

    /// <summary>Radial menu definitions for the current profile (saved with the template).</summary>
    public ObservableCollection<RadialMenuDefinition> RadialMenus => _radialMenus;

    public IProfileService GetProfileService() => _profileService;

    public void RefreshTemplates(string? preferredProfileId = null)
    {
        SelectedTemplate = _profileService.ReloadTemplates(preferredProfileId);
    }

    public void ReloadSelectedTemplate()
    {
        LoadSelectedTemplate();
    }

    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null)
        => _keyboardCaptureService.TryCaptureKeyboardKey(key, systemKey);

    public void CancelKeyboardKeyRecording()
        => _keyboardCaptureService.CancelCapture();

    private void OnChildPanelConfigurationChanged(object? sender, EventArgs e)
    {
        MappingCount = Mappings.Count;
    }

    private void RefreshRadialDefinitionsInEngine()
    {
        if (_mappingEngine == null) return;
        var template = SelectedTemplate != null ? _profileService.LoadSelectedTemplate(SelectedTemplate) : null;
        _mappingEngine.SetRadialMenuDefinitions(
            _radialMenus.Count == 0 ? null : _radialMenus.ToList(),
            _keyboardActions.Count == 0 ? null : _keyboardActions.ToList(),
            template);
    }

    private void OnRadialMenusCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            RefreshRadialDefinitionsInEngine();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (RadialMenuDefinition rm in e.OldItems)
                rm.Items.CollectionChanged -= OnRadialMenuItemsCollectionChanged;
        }

        if (e.NewItems != null)
        {
            foreach (RadialMenuDefinition rm in e.NewItems)
                rm.Items.CollectionChanged += OnRadialMenuItemsCollectionChanged;
        }

        RefreshRadialDefinitionsInEngine();
    }

    private void OnRadialMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshRadialDefinitionsInEngine();

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
        var selectedProfileId = SelectedTemplate?.StorageKey;
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
            var localizedName = string.Empty;
            if (targetId.Length > 0 && _profileService.TryResolveTemplateLocation(targetId, out var loc))
            {
                localizedName = AvailableTemplates.FirstOrDefault(t => t.MatchesLocation(loc))?.DisplayName
                    ?? string.Empty;
            }
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

