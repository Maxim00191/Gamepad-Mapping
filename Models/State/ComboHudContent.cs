namespace GamepadMapperGUI.Models;

public sealed record ComboHudLine(string Primary, string? Detail);

public sealed record ComboHudContent(string Title, IReadOnlyList<ComboHudLine> Lines);
