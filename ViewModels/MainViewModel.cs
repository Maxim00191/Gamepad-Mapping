using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

namespace Gamepad_Mapping.ViewModels;

public class TemplateOption
{
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ProfileService _profileService;
    private readonly GamepadReader _gamepadReader;

    public MainViewModel()
    {
        // Ensure property updates land on the UI thread (gamepad events originate on a background thread).
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _profileService = new ProfileService();
        _gamepadReader = new GamepadReader();

        AvailableTemplates = new ObservableCollection<TemplateOption>();
        Mappings = new ObservableCollection<MappingEntry>();

        _gamepadReader.OnButtonPressed += buttons =>
            DispatchToUi(() => LastButtonPressed = buttons.ToString());
        _gamepadReader.OnButtonReleased += buttons =>
            DispatchToUi(() => LastButtonReleased = buttons.ToString());
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

        StartGamepad();
    }

    private void DispatchToUi(Action action)
    {
        if (action is null) return;
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    private void LoadTemplates()
    {
        AvailableTemplates.Clear();

        var templatesDir = _profileService.LoadTemplateDirectory();
        if (!Directory.Exists(templatesDir))
            return;

        var jsonFiles = Directory.GetFiles(templatesDir, "*.json", SearchOption.TopDirectoryOnly);
        var options = new System.Collections.Generic.List<TemplateOption>();

        foreach (var file in jsonFiles)
        {
            var gameId = Path.GetFileNameWithoutExtension(file);
            try
            {
                var template = _profileService.LoadTemplate(gameId);
                options.Add(new TemplateOption { GameId = gameId, DisplayName = template.DisplayName });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load template '{gameId}': {ex.Message}");
            }
        }

        foreach (var opt in options.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase))
            AvailableTemplates.Add(opt);

        SelectedTemplate = AvailableTemplates.FirstOrDefault(t => t.GameId == _profileService.DefaultGameId)
                            ?? AvailableTemplates.FirstOrDefault();
    }

    private void LoadSelectedTemplate()
    {
        if (SelectedTemplate is null)
            return;

        var template = _profileService.LoadTemplate(SelectedTemplate.GameId);

        CurrentTemplateDisplayName = template.DisplayName;

        Mappings.Clear();
        foreach (var mapping in template.Mappings)
            Mappings.Add(mapping);

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
            Debug.WriteLine($"Failed to load template '{value?.GameId}': {ex.Message}");
        }
    }

    [ObservableProperty]
    private ObservableCollection<MappingEntry> mappings;

    [ObservableProperty]
    private string currentTemplateDisplayName = string.Empty;

    [ObservableProperty]
    private int mappingCount;

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

    [RelayCommand]
    private void ReloadTemplate()
    {
        if (SelectedTemplate is null)
            return;

        LoadSelectedTemplate();
    }

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
}

