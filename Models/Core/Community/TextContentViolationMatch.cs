#nullable enable

namespace GamepadMapperGUI.Models.Core.Community;

public sealed record TextContentViolationMatch(
    string ContextLabel,
    string FieldCaption,
    string PatternId,
    string? SuggestionResourceKey = null);
