#nullable enable

using System.Collections.ObjectModel;

namespace GamepadMapperGUI.Services.Automation;

internal sealed class AutomationRunLogBuffer : Collection<string>
{
    private readonly IProgress<string>? _lineProgress;

    public AutomationRunLogBuffer(IProgress<string>? lineProgress) =>
        _lineProgress = lineProgress;

    protected override void InsertItem(int index, string item)
    {
        base.InsertItem(index, item);
        _lineProgress?.Report(item);
    }
}
