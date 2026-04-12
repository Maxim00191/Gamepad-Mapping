using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

/// <summary>
/// Unified interface for executing actions defined in the keyboard catalog.
/// </summary>
internal interface IKeyboardActionExecutor
{
    /// <summary>
    /// Executes the specified action definition.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="sourceToken">The source of the trigger (e.g. "RadialMenu").</param>
    /// <param name="errorStatus">Out parameter for error reporting.</param>
    /// <returns>True if execution was successful.</returns>
    bool Execute(KeyboardActionDefinition action, string sourceToken, out string? errorStatus);
}
