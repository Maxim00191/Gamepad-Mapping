using System.Collections.Generic;

namespace GamepadMapperGUI.Interfaces.Core;

/// <summary>
/// Represents a catalog of keyboard and template actions that can be referenced by ID.
/// </summary>
public interface IKeyboardActionCatalog
{
    /// <summary>
    /// Resolves an action by its unique identifier.
    /// </summary>
    /// <param name="actionId">The action ID to look up.</param>
    /// <returns>The action definition if found; otherwise null.</returns>
    Models.KeyboardActionDefinition? GetAction(string actionId);

    /// <summary>
    /// Gets all available action definitions in the catalog.
    /// </summary>
    IEnumerable<Models.KeyboardActionDefinition> GetAllActions();
}
