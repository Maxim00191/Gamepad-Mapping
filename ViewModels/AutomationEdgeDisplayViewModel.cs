#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Interfaces.Services.Automation;

namespace Gamepad_Mapping.ViewModels;

public sealed partial class AutomationEdgeDisplayViewModel : ObservableObject
{
    private readonly IAutomationEdgeGeometryBuilder _geometryBuilder;

    public AutomationEdgeDisplayViewModel(IAutomationEdgeGeometryBuilder geometryBuilder)
    {
        _geometryBuilder = geometryBuilder;
        _pathData = _geometryBuilder.BuildPathData(0d, 0d, 0d, 0d);
    }

    public Guid EdgeId { get; init; }

    public string SourcePortId { get; init; } = "";

    public string TargetPortId { get; init; } = "";

    [ObservableProperty]
    private double _fromX;

    [ObservableProperty]
    private double _fromY;

    [ObservableProperty]
    private double _toX;

    [ObservableProperty]
    private double _toY;

    [ObservableProperty]
    private bool _isCycleWarning;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private string _pathData = "";

    partial void OnFromXChanged(double value) => RebuildPathData();
    partial void OnFromYChanged(double value) => RebuildPathData();
    partial void OnToXChanged(double value) => RebuildPathData();
    partial void OnToYChanged(double value) => RebuildPathData();

    private void RebuildPathData()
    {
        PathData = _geometryBuilder.BuildPathData(FromX, FromY, ToX, ToY);
    }
}
