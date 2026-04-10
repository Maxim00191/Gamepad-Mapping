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
using GamepadMapperGUI.Interfaces;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services;
using Gamepad_Mapping.Utils;
using Gamepad_Mapping.Views;
using ElevationHandlerService = GamepadMapperGUI.Utils.ElevationHandler;

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
    private readonly PresentationOrchestrator _presentationOrchestrator;
    private readonly SettingsOrchestrator _settingsOrchestrator;
    private readonly ProfileOrchestrator _profileOrchestrator;
    private readonly EventHandler _profilesLoadedHandler;
    private readonly EventHandler<AppStatusChangedEventArgs> _appStatusChangedHandler;
    private IReadOnlyList<MappingEntry> _mappingsSnapshot = Array.Empty<MappingEntry>();

    private readonly ObservableCollection<KeyboardActionDefinition> _keyboardActions = new();
    private readonly ObservableCollection<RadialMenuDefinition> _radialMenus = new();

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
        IAppToastService? appToastService = null,
        IXInput? xinput = null)
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var settingsServiceInstance = settingsService ?? new SettingsService();
        _settingsOrchestrator = new SettingsOrchestrator(settingsServiceInstance);
        var appSettings = _settingsOrchestrator.Settings;

        _profileService = profileService ?? new ProfileService(settingsService: settingsServiceInstance, appSettings: appSettings);
        _processTargetService = processTargetService ?? new ProcessTargetService();
        _profileOrchestrator = new ProfileOrchestrator(_profileService, _processTargetService);
        _profileOrchestrator.TemplateLoaded += OnTemplateLoaded;
        _profileOrchestrator.TemplateSwitchRequested += ShowTemplateSwitchHud;

        _localFileService = localFileService ?? new LocalFileService();
        var sharedGitHubContentService = gitHubContentService ?? new GitHubContentService();
        var resolvedUpdateVersionCacheService = updateVersionCacheService ?? new UpdateVersionCacheService();
        _communityService = communityService ?? new CommunityTemplateService(_profileService, sharedGitHubContentService, _localFileService);
        _updateService = updateService ?? new UpdateService(sharedGitHubContentService, settingsServiceInstance, appSettings, resolvedUpdateVersionCacheService);
        _updateInstallerService = updateInstallerService ?? new UpdateInstallerService();
        _updateNotificationService = updateNotificationService ?? new UpdateNotificationService();
        _appToastService = appToastService ?? new AppToastService();
        _toastHost = new AppToastViewModel(_appToastService);
        var resolvedTrustedUtcTimeService = trustedUtcTimeService ?? new TrustedUtcTimeService();
        var resolvedUpdateQuotaPolicyProvider = updateQuotaPolicyProvider ?? new StaticUpdateQuotaPolicyProvider();
        var resolvedUpdateQuotaService = updateQuotaService ?? new UpdateQuotaService(resolvedUpdateQuotaPolicyProvider, resolvedTrustedUtcTimeService);

        UpdatePanel = new UpdateViewModel(
            _updateService,
            settingsServiceInstance,
            appSettings,
            _localFileService,
            _updateInstallerService,
            resolvedUpdateQuotaService,
            resolvedUpdateVersionCacheService);

        var baseDeadzone = appSettings.ThumbstickDeadzone;
        static float ResolveStickDeadzone(float specific, float shared)
        {
            var value = specific > 0f ? specific : shared;
            return Math.Clamp(value, 0f, 0.9f);
        }

        var initialLeftDeadzone = ResolveStickDeadzone(appSettings.LeftThumbstickDeadzone, baseDeadzone);
        var initialRightDeadzone = ResolveStickDeadzone(appSettings.RightThumbstickDeadzone, baseDeadzone);

        _gamepadReader = gamepadReader ??
            new GamepadReader(
                xinput ?? new XInputService(),
                initialLeftDeadzone,
                initialRightDeadzone,
                appSettings.LeftTriggerInnerDeadzone,
                appSettings.LeftTriggerOuterDeadzone,
                appSettings.RightTriggerInnerDeadzone,
                appSettings.RightTriggerOuterDeadzone);
        _keyboardCaptureService = keyboardCaptureService ?? new KeyboardCaptureService();
        _elevationHandler = elevationHandler ?? new ElevationHandlerService(_processTargetService);
        _appStatusMonitor = appStatusMonitor ?? new AppStatusMonitor(_processTargetService, _elevationHandler, initialGracePeriodMs: appSettings.FocusGracePeriodMs);
        _presentationOrchestrator = new PresentationOrchestrator(_dispatcher);

        InitializeUiSettings(appSettings);

        _profilesLoadedHandler = (_, _) => OnPropertyChanged(nameof(AvailableTemplates));
        _appStatusChangedHandler = (_, args) =>
            DispatchToUi(() => _presentationOrchestrator.UpdateStatus(args.State, args.StatusText));

        Mappings = new ObservableCollection<MappingEntry>();
        AvailableGamepadButtons = new ObservableCollection<string>(GamepadChordSegmentCatalog.AllSegmentNames);
        AvailableTriggerModes = new ObservableCollection<TriggerMoment>(Enum.GetValues<TriggerMoment>());
        Mappings.CollectionChanged += (_, _) =>
        {
            UpdateMappingCount();
            _mappingsSnapshot = Mappings.ToList();
        };

        InitializeChildViewModels(initialLeftDeadzone, initialRightDeadzone, appSettings);

        _mappingEngine = mappingEngine ?? CreateMappingEngine();
        _keyboardActions.CollectionChanged += (_, _) => RefreshRadialDefinitionsInEngine();
        _radialMenus.CollectionChanged += OnRadialMenusCollectionChanged;
        _profileService.ProfilesLoaded += _profilesLoadedHandler;
        _appStatusMonitor.StatusChanged += _appStatusChangedHandler;

        _gamepadReader.OnInputFrame += HandleInputFrame;

        _profileOrchestrator.LoadSelectedTemplate();

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

    public ObservableCollection<TemplateOption> AvailableTemplates => _profileOrchestrator.AvailableTemplates;

    public TemplateOption? SelectedTemplate
    {
        get => _profileOrchestrator.SelectedTemplate;
        set => _profileOrchestrator.SelectedTemplate = value;
    }

    public string CurrentTemplateDisplayName
    {
        get => _profileOrchestrator.CurrentTemplateDisplayName;
        set => _profileOrchestrator.CurrentTemplateDisplayName = value;
    }

    public string CurrentTemplateProfileId
    {
        get => _profileOrchestrator.CurrentTemplateProfileId;
        set => _profileOrchestrator.CurrentTemplateProfileId = value;
    }

    public string CurrentTemplateTemplateGroupId
    {
        get => _profileOrchestrator.CurrentTemplateTemplateGroupId;
        set => _profileOrchestrator.CurrentTemplateTemplateGroupId = value;
    }

    public string CurrentTemplateAuthor
    {
        get => _profileOrchestrator.CurrentTemplateAuthor;
        set => _profileOrchestrator.CurrentTemplateAuthor = value;
    }

    public string CurrentTemplateCatalogFolder
    {
        get => _profileOrchestrator.CurrentTemplateCatalogFolder;
        set => _profileOrchestrator.CurrentTemplateCatalogFolder = value;
    }

    public string TemplateTargetProcessName
    {
        get => _profileOrchestrator.TemplateTargetProcessName;
        set => _profileOrchestrator.TemplateTargetProcessName = value;
    }

    public IReadOnlyList<string>? ComboLeadButtonsPersist => _profileOrchestrator.ComboLeadButtonsPersist;

    [ObservableProperty]
    private ObservableCollection<MappingEntry> mappings;

    [ObservableProperty]
    private ObservableCollection<string> availableGamepadButtons;

    [ObservableProperty]
    private ObservableCollection<TriggerMoment> availableTriggerModes;

    [ObservableProperty]
    private MappingEntry? selectedMapping;

    [ObservableProperty]
    private int mappingCount;

    [ObservableProperty]
    private bool isGamepadRunning;

    /// <summary>Left workspace: 0 mappings, 1 keyboard actions, 2 radial menus.</summary>
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

    public string TargetStatusText => _presentationOrchestrator.TargetStatusText;

    public AppTargetingState TargetState => _presentationOrchestrator.TargetState;

    private void OnTemplateLoaded(GameProfileTemplate? template)
    {
        if (template is null) return;

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

        ApplyDeclaredProcessTarget();
        UpdateTemplateToggleDisplayNames();

        CatalogPanel.SelectedKeyboardAction = null;
        CatalogPanel.SelectedRadialMenu = null;
        CatalogPanel.SelectedRadialSlot = null;
        RefreshRightPanelSurface();
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

    private void SyncAppStatusMonitor()
    {
        _appStatusMonitor.UpdateTarget(SelectedTargetProcess, IsProcessTargetingEnabled);
    }

    partial void OnSelectedTargetProcessChanged(ProcessInfo? value)
    {
        if (value is not null)
            _elevationHandler.CheckAndPromptElevation(value);

        IsProcessTargetingEnabled = value is not null;
        SyncAppStatusMonitor();
    }

    partial void OnIsProcessTargetingEnabledChanged(bool value) => SyncAppStatusMonitor();

    [ObservableProperty]
    private int focusGracePeriodMsSetting;

    partial void OnFocusGracePeriodMsSettingChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 5000);
        if (clamped != value)
            focusGracePeriodMsSetting = clamped;
        _settingsOrchestrator.Settings.FocusGracePeriodMs = clamped;
        _settingsOrchestrator.SaveSettings();
        _appStatusMonitor.UpdateGracePeriod(clamped);
    }

    [ObservableProperty]
    private int modifierGraceMsSetting;

    partial void OnModifierGraceMsSettingChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 10_000);
        if (clamped != value)
            modifierGraceMsSetting = clamped;
        _settingsOrchestrator.Settings.ModifierGraceMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int leadKeyReleaseSuppressMsSetting;

    partial void OnLeadKeyReleaseSuppressMsSettingChanged(int value)
    {
        var clamped = Math.Clamp(value, 50, 10_000);
        if (clamped != value)
            leadKeyReleaseSuppressMsSetting = clamped;
        _settingsOrchestrator.Settings.LeadKeyReleaseSuppressMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int gamepadPollingIntervalMs;

    partial void OnGamepadPollingIntervalMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 5, 30);
        if (clamped != value)
            gamepadPollingIntervalMs = clamped;
        _settingsOrchestrator.Settings.GamepadPollingIntervalMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int radialMenuConfirmModeIndex;

    partial void OnRadialMenuConfirmModeIndexChanged(int value)
    {
        var clamped = value < 0 ? 0 : (value > 1 ? 1 : value);
        if (clamped != value)
            radialMenuConfirmModeIndex = clamped;
        _settingsOrchestrator.Settings.RadialMenuConfirmMode = clamped == 0 ? "returnStickToCenter" : "releaseGuideKey";
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int radialMenuHudLabelModeIndex;

    partial void OnRadialMenuHudLabelModeIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
            radialMenuHudLabelModeIndex = clamped;
        _settingsOrchestrator.Settings.RadialMenuHudLabelMode =
            RadialMenuHudLabelModeParser.ToSettingString((RadialMenuHudLabelMode)clamped);
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private double radialHudScaleSetting;

    partial void OnRadialHudScaleSettingChanged(double value)
    {
        var clamped = RadialHudLayout.ClampHudScale(value);
        if (Math.Abs(clamped - value) > 1e-6)
            radialHudScaleSetting = clamped;
        RadialHudLayout.HudScale = clamped;
        _settingsOrchestrator.Settings.RadialHudScale = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private float defaultAnalogActivationThreshold;

    partial void OnDefaultAnalogActivationThresholdChanged(float value)
    {
        var clamped = Math.Clamp(value, 0.01f, 1f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            defaultAnalogActivationThreshold = clamped;
        _settingsOrchestrator.Settings.DefaultAnalogActivationThreshold = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private float mouseLookSensitivity;

    partial void OnMouseLookSensitivityChanged(float value)
    {
        var clamped = Math.Clamp(value, 1f, 100f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            mouseLookSensitivity = clamped;
        _settingsOrchestrator.Settings.MouseLookSensitivity = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private float analogChangeEpsilon;

    partial void OnAnalogChangeEpsilonChanged(float value)
    {
        var clamped = Math.Clamp(value, 0.001f, 0.1f);
        if (Math.Abs(clamped - value) > float.Epsilon)
            analogChangeEpsilon = clamped;
        _settingsOrchestrator.Settings.AnalogChangeEpsilon = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int keyboardTapHoldDurationMs;

    partial void OnKeyboardTapHoldDurationMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 20, 50);
        if (clamped != value)
            keyboardTapHoldDurationMs = clamped;
        _settingsOrchestrator.Settings.KeyboardTapHoldDurationMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int tapInterKeyDelayMs;

    partial void OnTapInterKeyDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1000);
        if (clamped != value)
            tapInterKeyDelayMs = clamped;
        _settingsOrchestrator.Settings.TapInterKeyDelayMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private int textInterCharDelayMs;

    partial void OnTextInterCharDelayMsChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1000);
        if (clamped != value)
            textInterCharDelayMs = clamped;
        _settingsOrchestrator.Settings.TextInterCharDelayMs = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    [ObservableProperty]
    private ComboHudPlacement comboHudPlacementSetting;

    partial void OnComboHudPlacementSettingChanged(ComboHudPlacement value)
    {
        _settingsOrchestrator.Settings.ComboHudPlacement = value.ToString();
        _settingsOrchestrator.SaveSettings();
    }

    public ObservableCollection<UiLanguageOption> AvailableUiLanguages => _settingsOrchestrator.AvailableUiLanguages;

    public UiLanguageOption? SelectedUiLanguage
    {
        get => _settingsOrchestrator.SelectedUiLanguage;
        set => _settingsOrchestrator.SelectedUiLanguage = value;
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
            _mappingEngine.InvalidateComboHudPresentation();
            ShowTemplateSwitchHud(opt.DisplayName);
        }

        _mappingEngine.ForceReleaseAllOutputs();
        _mappingEngine.ForceReleaseAnalogOutputs();
        SelectedTemplate = opt;
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
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage(launchArgs.Value.ReleaseTag),
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
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage(p.ReleaseTag),
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
                Title = _settingsOrchestrator.Localize("UpdateFailedToastTitle"),
                Message = p.ErrorMessage ?? _settingsOrchestrator.Localize("UpdateInstallLaunchFailed"),
                AutoHideSeconds = null,
                OnClosed = () => _updateNotificationService.AcknowledgeFailure(),
                InvokeOnClosedWhenExitingApplication = true
            });
            return;
        }

        if (!_settingsOrchestrator.Settings.HasSeenWelcomeToast)
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("WelcomeToastTitle"),
                Message = _settingsOrchestrator.Localize("WelcomeToastMessage"),
                AutoHideSeconds = null,
                OnClosed = () =>
                {
                    _settingsOrchestrator.Settings.HasSeenWelcomeToast = true;
                    _settingsOrchestrator.SaveSettings();
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
                Title = _settingsOrchestrator.Localize("UpdateSuccessToastTitle"),
                Message = _settingsOrchestrator.FormatUpdateSuccessMessage("vDebugSuccess"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        if (args.Any(a => string.Equals(a, "--debug-update-failed", StringComparison.OrdinalIgnoreCase)))
        {
            _appToastService.Show(new AppToastRequest
            {
                Title = _settingsOrchestrator.Localize("UpdateFailedToastTitle"),
                Message = _settingsOrchestrator.Localize("UpdateFailedToastMessage"),
                AutoHideSeconds = null,
                InvokeOnClosedWhenExitingApplication = false
            });
            return true;
        }

        return false;
    }
#endif

    public void Dispose()
    {
        try
        {
            _presentationOrchestrator.Dispose();
            _gamepadReader.OnInputFrame -= HandleInputFrame;
            _gamepadReader.Stop();
            _appToastService.NotifyApplicationExiting();
            _toastHost.Dispose();
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

    private void HandleInputFrame(InputFrame frame)
    {
        var allow = _appStatusMonitor.EvaluateNow();
        var (leftDz, rightDz) = _gamepadReader is GamepadReader gr 
            ? (gr.LeftThumbstickDeadzone, gr.RightThumbstickDeadzone)
            : (GamepadMonitorPanel.LeftThumbstickDeadzone, GamepadMonitorPanel.RightThumbstickDeadzone);

        var result = _mappingEngine.ProcessInputFrame(frame, _mappingsSnapshot, allow);
        GamepadMonitorPanel.RecordInputFrameSnapshot(frame, result, leftDz, rightDz);
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
            if (!GamepadMonitorPanel.IsHudEnabled)
            {
                _presentationOrchestrator.HideAllHuds();
                return;
            }

            var a = (byte)Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220);
            var o = Math.Clamp(GamepadMonitorPanel.ComboHudShadowOpacity, 0.08, 0.60);
            _presentationOrchestrator.ShowComboHud(content, a, o, ComboHudPlacementSetting);
        }, DispatcherPriority.Input);
    }

    private void ShowTemplateSwitchHud(string profileDisplayName)
    {
        if (!GamepadMonitorPanel.IsHudEnabled)
            return;

        var seconds = Math.Clamp(GamepadMonitorPanel.TemplateSwitchHudSeconds, 0.5, 5.0);
        var a = (byte)Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220);
        var o = Math.Clamp(GamepadMonitorPanel.ComboHudShadowOpacity, 0.08, 0.60);

        _presentationOrchestrator.ShowTemplateSwitchHud(
            profileDisplayName, 
            seconds, 
            a, 
            o, 
            ComboHudPlacementSetting,
            onFinished: () =>
            {
                _mappingEngine.InvalidateComboHudPresentation();
                _mappingEngine.RefreshComboHud();
            });
    }

    private void OnComboHudChromeChanged(int panelAlpha, double shadowOpacity)
    {
        var a = Math.Clamp(panelAlpha, 24, 220);
        var o = Math.Clamp(shadowOpacity, 0.08, 0.60);
        _settingsOrchestrator.Settings.ComboHudPanelAlpha = a;
        _settingsOrchestrator.Settings.ComboHudShadowOpacity = o;
        _settingsOrchestrator.SaveSettings();

        _presentationOrchestrator.ApplyHudVisuals(a, o);
    }

    private void OnTemplateSwitchHudSecondsChanged(double seconds)
    {
        var clamped = Math.Clamp(seconds, 0.5, 5.0);
        _settingsOrchestrator.Settings.TemplateSwitchHudSeconds = clamped;
        _settingsOrchestrator.SaveSettings();
    }

    private void OnHudEnabledChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            _presentationOrchestrator.HideAllHuds();
            GamepadMonitorPanel.ComboHudGateHint = string.Empty;
        }
    }

    public ObservableCollection<KeyboardActionDefinition> KeyboardActions => _keyboardActions;

    public ObservableCollection<RadialMenuDefinition> RadialMenus => _radialMenus;

    public IProfileService GetProfileService() => _profileService;

    public IKeyboardCaptureService KeyboardCaptureService => _keyboardCaptureService;

    public void RefreshTemplates(string? preferredProfileId = null)
        => _profileOrchestrator.RefreshTemplates(preferredProfileId);

    public void ReloadSelectedTemplate()
        => _profileOrchestrator.LoadSelectedTemplate();

    public bool TryCaptureKeyboardKey(Key key, Key? systemKey = null)
        => _keyboardCaptureService.TryCaptureKeyboardKey(key, systemKey);

    public void CancelKeyboardKeyRecording()
        => _keyboardCaptureService.CancelCapture();

    private void OnChildPanelConfigurationChanged(object? sender, EventArgs e) => UpdateMappingCount();

    private void UpdateMappingCount() => MappingCount = Mappings.Count;

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
        UpdateGamepadSettings(s =>
        {
            s.LeftThumbstickDeadzone = left;
            s.RightThumbstickDeadzone = right;
        });

        if (_gamepadReader is GamepadReader gr)
        {
            gr.LeftThumbstickDeadzone = left;
            gr.RightThumbstickDeadzone = right;
        }
    }

    private void OnTriggerDeadzonesChanged(float leftInner, float leftOuter, float rightInner, float rightOuter)
    {
        UpdateGamepadSettings(s =>
        {
            s.LeftTriggerInnerDeadzone = leftInner;
            s.LeftTriggerOuterDeadzone = leftOuter;
            s.RightTriggerInnerDeadzone = rightInner;
            s.RightTriggerOuterDeadzone = rightOuter;
        });

        if (_gamepadReader is GamepadReader gr)
        {
            gr.LeftTriggerInnerDeadzone = leftInner;
            gr.LeftTriggerOuterDeadzone = leftOuter;
            gr.RightTriggerInnerDeadzone = rightInner;
            gr.RightTriggerOuterDeadzone = rightOuter;
        }
    }

    private void UpdateGamepadSettings(Action<AppSettings> action)
    {
        action(_settingsOrchestrator.Settings);
        _settingsOrchestrator.SaveSettings();
    }

    private static ComboHudPlacement ParseComboHudPlacement(string? raw)
    {
        return Enum.TryParse<ComboHudPlacement>(raw, true, out var parsed)
            ? parsed
            : ComboHudPlacement.BottomRight;
    }

    private void ReloadLocalizedTemplateContent()
    {
        _profileOrchestrator.ReloadLocalizedContent();
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

    private static float ResolveStickDeadzone(float specific, float shared)
    {
        var value = specific > 0f ? specific : shared;
        return Math.Clamp(value, 0f, 0.9f);
    }

    private void InitializeUiSettings(AppSettings appSettings)
    {
        FocusGracePeriodMsSetting = appSettings.FocusGracePeriodMs;
        ModifierGraceMsSetting = appSettings.ModifierGraceMs;
        LeadKeyReleaseSuppressMsSetting = appSettings.LeadKeyReleaseSuppressMs;
        GamepadPollingIntervalMs = appSettings.GamepadPollingIntervalMs;
        RadialMenuConfirmModeIndex =
            string.Equals(appSettings.RadialMenuConfirmMode, "returnStickToCenter", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1;
        RadialMenuHudLabelModeIndex = (int)RadialMenuHudLabelModeParser.Parse(appSettings.RadialMenuHudLabelMode);
        RadialHudScaleSetting = RadialHudLayout.ClampHudScale(appSettings.RadialHudScale);
        DefaultAnalogActivationThreshold = appSettings.DefaultAnalogActivationThreshold;
        MouseLookSensitivity = appSettings.MouseLookSensitivity;
        AnalogChangeEpsilon = appSettings.AnalogChangeEpsilon;
        KeyboardTapHoldDurationMs = appSettings.KeyboardTapHoldDurationMs;
        TapInterKeyDelayMs = appSettings.TapInterKeyDelayMs;
        TextInterCharDelayMs = appSettings.TextInterCharDelayMs;
        ComboHudPlacementSetting = ParseComboHudPlacement(appSettings.ComboHudPlacement);
    }

    private void InitializeChildViewModels(float initialLeftDeadzone, float initialRightDeadzone, AppSettings appSettings)
    {
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
            initialLeftTriggerInnerDeadzone: appSettings.LeftTriggerInnerDeadzone,
            initialLeftTriggerOuterDeadzone: appSettings.LeftTriggerOuterDeadzone,
            initialRightTriggerInnerDeadzone: appSettings.RightTriggerInnerDeadzone,
            initialRightTriggerOuterDeadzone: appSettings.RightTriggerOuterDeadzone,
            triggerDeadzonesChanged: OnTriggerDeadzonesChanged,
            initialComboHudPanelAlpha: Math.Clamp(appSettings.ComboHudPanelAlpha, 24, 220),
            initialComboHudShadowOpacity: Math.Clamp(appSettings.ComboHudShadowOpacity, 0.08, 0.60),
            comboHudChromeChanged: OnComboHudChromeChanged,
            initialTemplateSwitchHudSeconds: Math.Clamp(appSettings.TemplateSwitchHudSeconds, 0.5, 5.0),
            templateSwitchHudChanged: OnTemplateSwitchHudSecondsChanged,
            uiDispatcher: _dispatcher);
        ProcessTargetPanel = new ProcessTargetPanelViewModel(this);
        ProfileTemplatePanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        NewBindingPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        MappingEditorPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
    }

    private IMappingEngine CreateMappingEngine()
    {
        return new MappingEngine(
            new KeyboardEmulator(),
            new MouseEmulator(),
            () => _appStatusMonitor.CanSendOutput,
            a => DispatchToUi(a),
            value => GamepadMonitorPanel.LastMappedOutput = value,
            value => GamepadMonitorPanel.LastMappingStatus = value,
            OnComboHud,
            _profileService.ModifierGraceMs,
            _profileService.LeadKeyReleaseSuppressMs,
            requestTemplateSwitchToProfileId: pid => DispatchToUi(() => _profileOrchestrator.RequestTemplateSwitch(pid)),
            profileService: _profileService,
            setComboHudGateHint: s => DispatchToUi(() => GamepadMonitorPanel.ComboHudGateHint = s ?? string.Empty),
            comboHudGateMessageFactory: _settingsOrchestrator.GetComboHudGateMessageFactory(),
            isComboHudPresentationSuppressed: () => _presentationOrchestrator.IsTemplateSwitchHudActive,
            radialMenuHud: new RadialMenuHudPresenter(
                () => (RadialMenuHudLabelMode)RadialMenuHudLabelModeIndex,
                () => Math.Clamp(GamepadMonitorPanel.ComboHudPanelAlpha, 24, 220)),
            getRadialMenuStickEngagementThreshold: () => DefaultAnalogActivationThreshold,
            getRadialMenuConfirmMode: () => RadialMenuConfirmModeIndex == 0
                ? RadialMenuConfirmMode.ReturnStickToCenter
                : RadialMenuConfirmMode.ReleaseGuideKey);
    }
}
