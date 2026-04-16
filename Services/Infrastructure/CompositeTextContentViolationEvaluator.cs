#nullable enable

using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Models.Core.Community;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class CompositeTextContentViolationEvaluator : ITextContentViolationEvaluator
{
    private readonly IReadOnlyList<ITextContentViolationEvaluator> _evaluators;

    public CompositeTextContentViolationEvaluator(params ITextContentViolationEvaluator[] evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        if (evaluators.Length == 0)
            throw new ArgumentException("At least one evaluator is required.", nameof(evaluators));

        _evaluators = evaluators;
    }

    public IReadOnlyList<TextContentViolationMatch> Evaluate(IReadOnlyList<TextContentInspectionField> fields)
    {
        if (fields.Count == 0)
            return [];

        var winners = new Dictionary<(string Context, string Caption), TextContentViolationMatch>();
        foreach (var evaluator in _evaluators)
        {
            foreach (var hit in evaluator.Evaluate(fields))
            {
                var key = (hit.ContextLabel, hit.FieldCaption);
                if (winners.ContainsKey(key))
                    continue;

                winners[key] = hit;
            }
        }

        var ordered = new List<TextContentViolationMatch>();
        foreach (var field in fields)
        {
            var key = (field.ContextLabel, field.FieldCaption);
            if (winners.TryGetValue(key, out var hit))
                ordered.Add(hit);
        }

        return ordered;
    }
}
