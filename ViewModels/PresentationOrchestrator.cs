using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Models.State;
using Gamepad_Mapping.Views;

namespace Gamepad_Mapping.ViewModels;

/// <summary>
/// Orchestrates the visual presentation layer, including HUD windows, status text, and language-aware formatting.
/// Merges responsibilities for Combo HUD, Template Switch HUD, and App Status presentation.
/// </summary>
public partial class PresentationOrchestrator : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private ComboHudWindow? _comboHudWindow;
    private TemplateSwitchHudWindow? _templateSwitchHudWindow;
    private DispatcherTimer? _templateSwitchHudTimer;
    
    [ObservableProperty]
    private string _targetStatusText = "No target selected - output suppressed";

    [ObservableProperty]
    private AppTargetingState _targetState = AppTargetingState.NoTargetSelected;

    [ObservableProperty]
    private bool _isTemplateSwitchHudActive;

    public PresentationOrchestrator(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void UpdateStatus(AppTargetingState state, string statusText)
    {
        _dispatcher.VerifyAccess();
        TargetState = state;
        TargetStatusText = statusText;
    }

    public void ShowComboHud(ComboHudContent? content, int panelAlpha, double shadowOpacity, ComboHudPlacement placement)
    {
        if (IsTemplateSwitchHudActive) return;

        if (content == null)
        {
            _comboHudWindow?.HideHud();
            return;
        }

        _comboHudWindow ??= new ComboHudWindow();
        _comboHudWindow.ShowHud(content, (byte)panelAlpha, shadowOpacity, placement);
    }

    public void ShowTemplateSwitchHud(string profileDisplayName, double displaySeconds, int panelAlpha, double shadowOpacity, ComboHudPlacement placement, Action onFinished)
    {
        if (_templateSwitchHudTimer != null)
        {
            _templateSwitchHudTimer.Stop();
            _templateSwitchHudTimer = null;
        }

        IsTemplateSwitchHudActive = true;
        
        // Hide combo HUD while switching
        _comboHudWindow?.HideHud();

        _templateSwitchHudTimer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(displaySeconds)
        };

        _templateSwitchHudTimer.Tick += (_, _) =>
        {
            _templateSwitchHudTimer?.Stop();
            _templateSwitchHudTimer = null;
            IsTemplateSwitchHudActive = false;
            _templateSwitchHudWindow?.HideHud();
            onFinished?.Invoke();
        };

        var title = "Profile switched";
        var line = new ComboHudLine($"→ {profileDisplayName}", null);
        var content = new ComboHudContent(title, new[] { line });

        _templateSwitchHudWindow ??= new TemplateSwitchHudWindow();
        _templateSwitchHudWindow.ShowHud(content, (byte)panelAlpha, shadowOpacity, placement);
        _templateSwitchHudTimer.Start();
    }

    public void HideAllHuds()
    {
        _dispatcher.Invoke(() =>
        {
            if (_templateSwitchHudTimer != null)
            {
                _templateSwitchHudTimer.Stop();
                _templateSwitchHudTimer = null;
            }
            IsTemplateSwitchHudActive = false;
            _comboHudWindow?.HideHud();
            _templateSwitchHudWindow?.HideHud();
        });
    }

    public void ApplyHudVisuals(int panelAlpha, double shadowOpacity)
    {
        _dispatcher.Invoke(() =>
        {
            if (_comboHudWindow is { IsVisible: true })
                _comboHudWindow.ApplyVisualSettings((byte)panelAlpha, shadowOpacity);
        });
    }

    public void Dispose()
    {
        _templateSwitchHudTimer?.Stop();
        _comboHudWindow?.Close();
        _templateSwitchHudWindow?.Close();
    }
}

