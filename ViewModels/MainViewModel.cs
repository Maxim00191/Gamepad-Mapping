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
    private readonly HashSet<GamepadButtons> _pressedButtons = [];
    private float _lastLeftTrigger;
    private float _lastRightTrigger;

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
        _gamepadReader = gamepadReader ?? new GamepadReader();
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
        RecentProcesses = new ObservableCollection<ProcessInfo>();
        Mappings = new ObservableCollection<MappingEntry>();
        AvailableGamepadButtons = new ObservableCollection<string>(
            Enum.GetNames<GamepadButtons>().Where(n => !string.Equals(n, nameof(GamepadButtons.None), StringComparison.OrdinalIgnoreCase)));
        AvailableTriggerModes = new ObservableCollection<TriggerMoment>(Enum.GetValues<TriggerMoment>());
        Mappings.CollectionChanged += (_, _) => MappingCount = Mappings.Count;

        ProfileTemplatePanel = new ProfileTemplatePanelViewModel(this);
        NewBindingPanel = new NewBindingPanelViewModel(this);
        MappingEditorPanel = new MappingEditorViewModel(this);
        GamepadMonitorPanel = new GamepadMonitorViewModel(StopGamepadCommand);
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
            value => GamepadMonitorPanel.LastMappingStatus = value);
        _profileService.ProfilesLoaded += _profilesLoadedHandler;
        _appStatusMonitor.StatusChanged += _appStatusChangedHandler;

        _gamepadReader.OnButtonPressed += buttons =>
            DispatchToUi(() =>
            {
                _pressedButtons.Add(buttons);
                GamepadMonitorPanel.LastButtonPressed = buttons.ToString();
                var snapshot = Mappings.ToList();
                _mappingEngine.HandleButtonMappings(buttons, _mappingEngine.ButtonPressedTrigger, _pressedButtons, snapshot, _lastLeftTrigger, _lastRightTrigger);
                _mappingEngine.HandleButtonMappings(buttons, _mappingEngine.ButtonTapTrigger, _pressedButtons, snapshot, _lastLeftTrigger, _lastRightTrigger);
            });

        _gamepadReader.OnButtonReleased += buttons =>
            DispatchToUi(() =>
            {
                GamepadMonitorPanel.LastButtonReleased = buttons.ToString();
                var preReleaseButtons = new HashSet<GamepadButtons>(_pressedButtons) { buttons };
                _mappingEngine.HandleButtonMappings(buttons, TriggerMoment.Released, preReleaseButtons, Mappings.ToList(), _lastLeftTrigger, _lastRightTrigger);
                _pressedButtons.Remove(buttons);
            });

        _gamepadReader.OnLeftThumbstickChanged += value =>
            DispatchToUi(() =>
            {
                GamepadMonitorPanel.LeftThumbX = value.X;
                GamepadMonitorPanel.LeftThumbY = value.Y;
                _mappingEngine.HandleThumbstickMappings(GamepadBindingType.LeftThumbstick, value, Mappings.ToList());
            });

        _gamepadReader.OnRightThumbstickChanged += value =>
            DispatchToUi(() =>
            {
                GamepadMonitorPanel.RightThumbX = value.X;
                GamepadMonitorPanel.RightThumbY = value.Y;
                _mappingEngine.HandleThumbstickMappings(GamepadBindingType.RightThumbstick, value, Mappings.ToList());
            });

        _gamepadReader.OnLeftTriggerChanged += value =>
            DispatchToUi(() =>
            {
                _lastLeftTrigger = value;
                GamepadMonitorPanel.LeftTrigger = value;
                _mappingEngine.HandleTriggerMappings(GamepadBindingType.LeftTrigger, value, Mappings.ToList());
            });

        _gamepadReader.OnRightTriggerChanged += value =>
            DispatchToUi(() =>
            {
                _lastRightTrigger = value;
                GamepadMonitorPanel.RightTrigger = value;
                _mappingEngine.HandleTriggerMappings(GamepadBindingType.RightTrigger, value, Mappings.ToList());
            });

        SelectedTemplate = _profileService.ReloadTemplates();
        LoadSelectedTemplate();

        RefreshProcesses();
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

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> recentProcesses;

    [ObservableProperty]
    private ProcessInfo? selectedTargetProcess;

    [ObservableProperty]
    private bool isProcessTargetingEnabled;

    [ObservableProperty]
    private string targetStatusText = "No target selected - output suppressed";

    [ObservableProperty]
    private AppTargetingState targetState = AppTargetingState.NoTargetSelected;

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


    [RelayCommand]
    private void StartGamepad()
    {
        if (IsGamepadRunning)
            return;

        _gamepadReader.Start();
        IsGamepadRunning = true;
        GamepadMonitorPanel.IsGamepadRunning = true;
    }

    [RelayCommand]
    private void StopGamepad()
    {
        if (!IsGamepadRunning)
            return;

        _gamepadReader.Stop();
        _mappingEngine.ForceReleaseAllOutputs();
        _mappingEngine.ForceReleaseAnalogOutputs();
        _pressedButtons.Clear();
        IsGamepadRunning = false;
        GamepadMonitorPanel.IsGamepadRunning = false;
    }

    public void Dispose()
    {
        try
        {
            _gamepadReader.Stop();
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

    private void LoadSelectedTemplate()
    {
        var template = _profileService.LoadSelectedTemplate(SelectedTemplate);
        if (template is null)
            return;

        CurrentTemplateDisplayName = template.DisplayName;

        Mappings.Clear();
        foreach (var mapping in template.Mappings)
            Mappings.Add(mapping);

        SelectedMapping = Mappings.FirstOrDefault();
        MappingCount = Mappings.Count;
    }

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
}

