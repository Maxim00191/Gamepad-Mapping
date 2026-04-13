using System.ComponentModel;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Orchestrates UI states, HUDs, and notifications.
/// </summary>
public interface IUiOrchestrator : IDisposable, INotifyPropertyChanged
{
    AppTargetingState TargetState { get; }
    string TargetStatusText { get; }
    
    void UpdateStatus(AppTargetingState state, string statusText);
    void ShowComboHud(ComboHudContent? content, byte alpha, double shadowOpacity, string placement);
    void ShowTemplateSwitchHud(string profileDisplayName, double seconds, byte alpha, double shadowOpacity, string placement, Action onFinished);
    void HideAllHuds();
    void ApplyHudVisuals(byte alpha, double shadowOpacity);
    
    void ShowToast(string title, string message, Action? onClosed = null);

    /// <summary>
    /// Shows a dialog for editing a mapping action.
    /// </summary>
    bool? ShowActionEditDialog(MappingEntry mapping);
}

