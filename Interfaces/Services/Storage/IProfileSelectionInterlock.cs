#nullable enable
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Storage;

/// <summary>
/// Allows the host to block or confirm profile template selection changes (e.g. unsaved workspace edits).
/// </summary>
public interface IProfileSelectionInterlock
{
    /// <summary>
    /// Called before the selected template changes to a different storage key.
    /// Return false to cancel the switch (caller should refresh bound UI).
    /// </summary>
    bool AllowSelectTemplate(TemplateOption? current, TemplateOption? proposed);

    /// <summary>Notify the view that the template picker selection must re-bind (e.g. switch was cancelled).</summary>
    void NotifySelectedTemplateBindingRefresh();
}
