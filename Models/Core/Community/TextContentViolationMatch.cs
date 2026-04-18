#nullable enable

namespace GamepadMapperGUI.Models.Core.Community;

public sealed record TextContentViolationMatch(
    string ContextLabel,
    string FieldCaption,
    string PatternId,
    string? SuggestionResourceKey = null,
    /// <summary>
    /// Policy-comparable substring that triggered the match (same normalization as the evaluator's internal haystack), for diagnostics/tests.
    /// </summary>
    string? MatchedSegmentHint = null,
    /// <summary>
    /// Full user field text that was inspected (original, not policy-normalized), capped for UI display.
    /// </summary>
    string? ViolatingFieldText = null);
