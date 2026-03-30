namespace GamepadMapperGUI.Models;

public sealed class ProcessInfo
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string MainWindowTitle { get; init; } = string.Empty;

    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(MainWindowTitle)
            ? ProcessName
            : $"{ProcessName}  —  {MainWindowTitle}";

    public override string ToString() => DisplayLabel;

    public override bool Equals(object? obj) =>
        obj is ProcessInfo other && ProcessId == other.ProcessId;

    public override int GetHashCode() => ProcessId;
}
