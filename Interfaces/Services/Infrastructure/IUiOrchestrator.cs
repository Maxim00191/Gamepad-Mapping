using System.ComponentModel;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Orchestrates UI states, HUDs, and notifications. HUD window entry points are thread-safe: work is queued to the UI
/// thread when the caller is not on the UI thread, without blocking the caller. In-game overlay HUDs are not gated by
/// tray-hidden state; <see cref="UpdateStatus"/> skips main-shell status churn while the primary window is in the tray.
/// </summary>
public interface IUiOrchestrator : IDisposable, INotifyPropertyChanged
{
    AppTargetingState TargetState { get; }
    string TargetStatusText { get; }
    
    /// <summary>Updates targeting status in the UI. Safe to call from any thread.</summary>
    void UpdateStatus(AppTargetingState state, string statusText);

    void ShowComboHud(ComboHudContent? content, byte alpha, double shadowOpacity, string placement);
    void ShowTemplateSwitchHud(string profileDisplayName, double seconds, byte alpha, double shadowOpacity, string placement, Action onFinished);
    void HideAllHuds();
    void ApplyHudVisuals(byte alpha, double shadowOpacity);
    
    void ShowToast(string title, string message, Action? onClosed = null);
}

