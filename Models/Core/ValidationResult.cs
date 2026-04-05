using System.Collections.Generic;
using System.Linq;

namespace GamepadMapperGUI.Models.Core;

public record ValidationResult(
    IEnumerable<string>? Errors = null,
    IEnumerable<string>? Warnings = null) : GamepadMapperGUI.Interfaces.Core.IValidationResult
{
    public bool IsValid => Errors == null || !Errors.Any();
    public IEnumerable<string> Errors { get; init; } = Errors ?? Enumerable.Empty<string>();
    public IEnumerable<string> Warnings { get; init; } = Warnings ?? Enumerable.Empty<string>();

    public static ValidationResult Success() => new();
    public static ValidationResult Failure(params string[] errors) => new(Errors: errors);
    public static ValidationResult Warning(params string[] warnings) => new(Warnings: warnings);
}
