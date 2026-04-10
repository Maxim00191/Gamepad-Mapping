using System.Collections.Generic;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IValidationResult
{
    bool IsValid { get; }
    IEnumerable<string> Errors { get; }
    IEnumerable<string> Warnings { get; }
}
