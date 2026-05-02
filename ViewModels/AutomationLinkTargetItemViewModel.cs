#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;

namespace Gamepad_Mapping.ViewModels;

public sealed partial class AutomationLinkTargetItemViewModel : ObservableObject
{
    public required Guid NodeId { get; init; }

    [ObservableProperty]
    private string _displayTitle = "";
}
