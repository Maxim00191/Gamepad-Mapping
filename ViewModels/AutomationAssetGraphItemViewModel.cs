#nullable enable

namespace Gamepad_Mapping.ViewModels;

public sealed class AutomationAssetGraphItemViewModel
{
    public required string DisplayLabel { get; init; }

    public required string RelativePath { get; init; }

    public required string FullPath { get; init; }
}
