#nullable enable

using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ITextContentViolationEvaluator
{
    IReadOnlyList<TextContentViolationMatch> Evaluate(IReadOnlyList<TextContentInspectionField> fields);
}
