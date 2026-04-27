using System;
using System.Collections.Generic;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Provides a unified source of truth for selection across different UI components.
/// </summary>
/// <typeparam name="T">The type of items being selected.</typeparam>
public interface ISelectionService<T> where T : class
{
    /// <summary>The primary selected item.</summary>
    T? SelectedItem { get; set; }

    /// <summary>All currently selected items (for multi-select).</summary>
    IReadOnlyList<T> SelectedItems { get; }

    /// <summary>Updates the selection from a collection of objects.</summary>
    void UpdateSelection(IEnumerable<object> items);

    /// <summary>Resets selection and raises <see cref="SelectionChanged"/> even when the item reference matches.</summary>
    void ResetTo(T? item);

    /// <summary>Selects all items in the workspace.</summary>
    void SelectAll(IEnumerable<T> allItems);

    /// <summary>Raised when the selection changes.</summary>
    event EventHandler SelectionChanged;
}
