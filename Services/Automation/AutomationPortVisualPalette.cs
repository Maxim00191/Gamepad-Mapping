#nullable enable

using System.Windows.Media;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public static class AutomationPortVisualPalette
{
    private static readonly Brush ExecutionStroke = Create("#2D9CDB");
    private static readonly Brush ExecutionFill = Create("#2D9CDB");
    private static readonly Brush BooleanStroke = Create("#27AE60");
    private static readonly Brush BooleanFill = Create("#27AE60");
    private static readonly Brush NumberStroke = Create("#F39C12");
    private static readonly Brush NumberFill = Create("#F39C12");
    private static readonly Brush StringStroke = Create("#9B59B6");
    private static readonly Brush StringFill = Create("#9B59B6");
    private static readonly Brush ImageStroke = Create("#8E44AD");
    private static readonly Brush ImageFill = Create("#8E44AD");
    private static readonly Brush AnyStroke = Create("#7F8C8D");
    private static readonly Brush AnyFill = Create("#7F8C8D");
    private static readonly Brush CandidateValid = Create("#2ECC71");
    private static readonly Brush CandidateInvalid = Create("#E74C3C");

    public static Brush GetBaseStroke(AutomationPortFlowKind flowKind, AutomationPortType portType)
    {
        if (flowKind == AutomationPortFlowKind.Execution)
            return ExecutionStroke;

        return portType switch
        {
            AutomationPortType.Boolean => BooleanStroke,
            AutomationPortType.Number => NumberStroke,
            AutomationPortType.String => StringStroke,
            AutomationPortType.ImageOrCoordinates => ImageStroke,
            AutomationPortType.Any => AnyStroke,
            _ => AnyStroke
        };
    }

    public static Brush GetBaseFill(AutomationPortFlowKind flowKind, AutomationPortType portType, bool isOutput)
    {
        if (!isOutput)
            return Brushes.Transparent;
        if (flowKind == AutomationPortFlowKind.Execution)
            return ExecutionFill;

        return portType switch
        {
            AutomationPortType.Boolean => BooleanFill,
            AutomationPortType.Number => NumberFill,
            AutomationPortType.String => StringFill,
            AutomationPortType.ImageOrCoordinates => ImageFill,
            AutomationPortType.Any => AnyFill,
            _ => AnyFill
        };
    }

    public static Brush GetCandidateStroke(bool isValid) => isValid ? CandidateValid : CandidateInvalid;

    private static Brush Create(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
