#nullable enable

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationComparisonEvaluator
{
    public const string GreaterThan = "gt";
    public const string LessThan = "lt";
    public const string EqualTo = "eq";

    public static string NormalizeOperator(string? operatorId, string defaultOperator = GreaterThan)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
            return defaultOperator;

        return operatorId.Trim().ToLowerInvariant() switch
        {
            GreaterThan or ">" or "greater_than" => GreaterThan,
            LessThan or "<" or "less_than" => LessThan,
            EqualTo or "==" or "=" or "equals" => EqualTo,
            _ => defaultOperator
        };
    }

    public static bool IsSupportedOperator(string? operatorId)
    {
        if (string.IsNullOrWhiteSpace(operatorId))
            return true;

        var normalized = NormalizeOperator(operatorId, string.Empty);
        return string.Equals(normalized, GreaterThan, StringComparison.Ordinal) ||
            string.Equals(normalized, LessThan, StringComparison.Ordinal) ||
            string.Equals(normalized, EqualTo, StringComparison.Ordinal);
    }

    public static string FromNodeType(string nodeTypeId) =>
        nodeTypeId switch
        {
            "logic.gt" => GreaterThan,
            "logic.lt" => LessThan,
            "logic.eq" => EqualTo,
            _ => GreaterThan
        };

    public static bool Evaluate(string? operatorId, double left, double right) =>
        NormalizeOperator(operatorId) switch
        {
            GreaterThan => left > right,
            LessThan => left < right,
            EqualTo => Math.Abs(left - right) < double.Epsilon,
            _ => false
        };
}
