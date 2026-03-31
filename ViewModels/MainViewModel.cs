using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private ComboHudWindow? _comboHudWindow;
    private readonly AppSettings _appSettings;

    public MainViewModel(
        IProfileService? profileService = null,
        IGamepadReader? gamepadReader = null,
        IProcessTargetService? processTargetService = null,
        IKeyboardCaptureService? keyboardCaptureService = null,
        IElevationHandler? elevationHandler = null,
        IAppStatusMonitor? appStatusMonitor = null,
        IMappingEngine? mappingEngine = null)
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _profileService = profileService ?? new ProfileService();

        _appSettings = SettingsService.LoadSettings();
        var baseDeadzone = _appSettings.ThumbstickDeadzone;
        static float ResolveStickDeadzone(float specific, float shared)
        {
            var value = specific > 0f ? specific : shared;
            return Math.Clamp(value, 0f, 0.9f);
        }

        var initialLeftDeadzone = ResolveStickDeadzone(_appSettings.LeftThumbstickDeadzone, baseDeadzone);
        var initialRightDeadzone = ResolveStickDeadzone(_appSettings.RightThumbstickDeadzone, baseDeadzone);

        _gamepadReader = gamepadReader ?? new GamepadReader(initialLeftDeadzone, initialRightDeadzone);
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
            deadzoneChanged: OnThumbstickDeadzoneChanged);
        ProcessTargetPanel = new ProcessTargetPanelViewModel(this);
        ProfileTemplatePanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        NewBindingPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        MappingEditorPanel.ConfigurationChanged += OnChildPanelConfigurationChanged;
        _mappingEngine = mappingEngine ?? new MappingEngine(
            new KeyboardEmulator(),
            new MouseEmulator(),
            CanDispatchMappedOutput,
            DispatchToUi,
            value => GamepadMonitorPanel.LastMappedOutput = value,
            value => GamepadMonitorPanel.LastMappingStatus = value,
            OnComboHud,
            _profileService.ModifierGraceMs,
            _profileService.LeadKeyReleaseSuppressMs);
        _profileService.ProfilesLoaded += _profilesLoadedHandler;
        _appStatusMonitor.StatusChanged += _appStatusChangedHandler;

        _gamepadReader.OnInputFrame += frame =>
            DispatchToUi(() =>
            {
                var leftDz = GamepadMonitorPanel.LeftThumbstickDeadzone;
                var rightDz = GamepadMonitorPanel.RightThumbstickDeadzone;
                static float ClampDeadzone(float v, float dz) => MathF.Abs(v) < dz ? 0f : v;

                GamepadMonitorPanel.LeftThumbX = ClampDeadzone(frame.LeftThumbstick.X, leftDz);
                GamepadMonitorPanel.LeftThumbY = ClampDeadzone(frame.LeftThumbstick.Y, leftDz);
                GamepadMonitorPanel.RightThumbX = ClampDeadzone(frame.RightThumbstick.X, rightDz);
                GamepadMonitorPanel.RightThumbY = ClampDeadzone(frame.RightThumbstick.Y, rightDz);
                GamepadMonitorPanel.LeftTrigger = frame.LeftTrigger;
                GamepadMonitorPanel.RightTrigger = frame.RightTrigger;

                var result = _mappingEngine.ProcessInputFrame(frame, _mappingsSnapshot);
                if (result.PressedButtons.Length > 0)
                    GamepadMonitorPanel.LastButtonPressed = result.PressedButtons[^1].ToString();
                if (result.ReleasedButtons.Length > 0)
                    GamepadMonitorPanel.LastButtonReleased = result.ReleasedButtons[^1].ToString();
            });

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
            LoadSelectedTemplate();
            _profileService.PersistLastSelectedTemplateProfileId(value?.ProfileId);
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

    private bool CanDispatchMappedOutput()
    {
        _appStatusMonitor.EvaluateNow();
        return _appStatusMonitor.CanSendOutput;
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
    
    private void DispatchToUi(Action action)
    {
        if (action is null) return;
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    private void OnComboHud(ComboHudContent? content)
    {
        DispatchToUi(() =>
        {
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
            _comboHudWindow.ShowHud(content);
        });
    }

    private void OnHudEnabledChanged(bool isEnabled)
    {
        if (!isEnabled)
            DispatchToUi(() => _comboHudWindow?.HideHud());
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

        TemplateTargetProcessName = template.TargetProcessName ?? string.Empty;
        ApplyDeclaredProcessTarget();
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
        SettingsService.SaveSettings(_appSettings);
    }
}

