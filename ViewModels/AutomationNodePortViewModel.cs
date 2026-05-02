#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models.Automation;
using System.Windows.Media;

namespace Gamepad_Mapping.ViewModels;

public enum AutomationPortVisualState
{
    Default = 0,
    CandidateValid = 1,
    CandidateInvalid = 2
}

public sealed partial class AutomationNodePortViewModel : ObservableObject
{
    public required string Id { get; init; }

    public string DisplayLabel { get; init; } = "";

    public required bool IsOutput { get; init; }

    public required AutomationPortFlowKind FlowKind { get; init; }

    public required AutomationPortType PortType { get; init; }

    [ObservableProperty]
    private Brush _baseStroke = Brushes.Transparent;

    [ObservableProperty]
    private Brush _baseFill = Brushes.Transparent;

    [ObservableProperty]
    private AutomationPortVisualState _visualState = AutomationPortVisualState.Default;

    [ObservableProperty]
    private bool _isHovered;
}
