using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

/// <summary>
/// Base class for different action editor strategies.
/// </summary>
public abstract partial class ActionEditorViewModelBase : ObservableObject
{
    protected bool _syncingFromMapping;

    public event EventHandler? ConfigurationChanged;

    public void NotifyConfigurationChanged() => ConfigurationChanged?.Invoke(this, EventArgs.Empty);

    public abstract void SyncFrom(MappingEntry mapping);

    /// <summary>
    /// Applies the editor state to the given mapping entry.
    /// </summary>
    /// <returns>True if the application was successful and valid.</returns>
    public abstract bool ApplyTo(MappingEntry mapping);

    /// <summary>
    /// Clears the editor state for a new mapping.
    /// </summary>
    public abstract void Clear();

    /// <summary>
    /// Resets common mapping fields to null/empty when switching action types.
    /// </summary>
    protected static void ResetCommonMappingFields(MappingEntry mapping)
    {
        mapping.ItemCycle = null;
        mapping.TemplateToggle = null;
        mapping.RadialMenu = null;
        mapping.ActionId = null;
        mapping.KeyboardKey = string.Empty;
        mapping.HoldKeyboardKey = string.Empty;
        mapping.HoldThresholdMs = null;
    }

    /// <summary>
    /// Called when UI localization changes so derived editors can refresh computed labels.
    /// </summary>
    public virtual void OnLocalizationChanged()
    {
    }
}
