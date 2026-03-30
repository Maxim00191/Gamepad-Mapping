namespace GamepadMapperGUI.Models;

public class TemplateOption
{
    public string ProfileId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}
