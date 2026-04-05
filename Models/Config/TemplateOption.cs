namespace GamepadMapperGUI.Models;

public class TemplateOption
{
    public string ProfileId { get; set; } = string.Empty;
    public string TemplateGroupId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public List<RadialMenuDefinition>? RadialMenus { get; set; }

    public override string ToString() => DisplayName;
}
