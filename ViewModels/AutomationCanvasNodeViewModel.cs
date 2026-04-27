#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models.Automation;
namespace Gamepad_Mapping.ViewModels;

public sealed partial class AutomationCanvasNodeViewModel : ObservableObject
{
    private readonly AutomationNodeState _state;

    public AutomationCanvasNodeViewModel(
        AutomationNodeState state,
        string title,
        string glyph,
        IReadOnlyList<AutomationNodePortViewModel> inputPorts,
        IReadOnlyList<AutomationNodePortViewModel> outputPorts)
    {
        _state = state;
        _title = title;
        _glyph = glyph;
        InputPorts = inputPorts ?? [];
        OutputPorts = outputPorts ?? [];
    }

    public AutomationNodeState State => _state;

    public Guid Id => _state.Id;

    public string NodeTypeId => _state.NodeTypeId;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _glyph;

    [ObservableProperty]
    private AutomationCanvasVisualState _visualState = AutomationCanvasVisualState.Default;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private double _nodeVisualWidth = 280d;

    [ObservableProperty]
    private double _outputPortRowMinHeight = 28d;

    [ObservableProperty]
    private double _inputPortRowMinHeight = 28d;

    [ObservableProperty]
    private double _settingsSectionMinHeight = 0d;

    [ObservableProperty]
    private double _nodeVisualMinHeight = 152d;

    [ObservableProperty]
    private double _nodeContentMinWidth = 156d;

    public IReadOnlyList<AutomationNodePortViewModel> InputPorts { get; }

    public IReadOnlyList<AutomationNodePortViewModel> OutputPorts { get; }

    public IList<AutomationInlineNodeFieldViewModel> InlineEditors { get; } = [];

    public bool HasInlineEditors => InlineEditors.Count > 0;

    public double EstimatedVisualHeight => NodeVisualMinHeight;

    public double X
    {
        get => _state.X;
        set
        {
            if (double.Abs(_state.X - value) < 0.01)
                return;

            _state.X = value;
            OnPropertyChanged(nameof(X));
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double Y
    {
        get => _state.Y;
        set
        {
            if (double.Abs(_state.Y - value) < 0.01)
                return;

            _state.Y = value;
            OnPropertyChanged(nameof(Y));
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? PositionChanged;

    public void ApplyLayoutMetrics(AutomationNodeLayoutMetrics metrics)
    {
        NodeVisualWidth = metrics.NodeWidth;
        OutputPortRowMinHeight = metrics.OutputPortRowMinHeight;
        InputPortRowMinHeight = metrics.InputPortRowMinHeight;
        SettingsSectionMinHeight = metrics.SettingsSectionMinHeight;
        NodeVisualMinHeight = metrics.VisualMinHeight;
        NodeContentMinWidth = metrics.ContentMinWidth;
    }
}
