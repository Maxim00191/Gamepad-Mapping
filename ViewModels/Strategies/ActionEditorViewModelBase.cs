using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models;

namespace Gamepad_Mapping.ViewModels.Strategies;

/// <summary>
/// Base class for different action editor strategies.
/// </summary>
public abstract partial class ActionEditorViewModelBase : ObservableObject
{
    /// <summary>
    /// Syncs the editor state from the given mapping entry.
    /// </summary>
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
}
