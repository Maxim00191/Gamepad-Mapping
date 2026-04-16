#nullable enable

namespace GamepadMapperGUI.Models.Core.Community;

public sealed class UploadTextPolicyPattern
{
    public string Id { get; set; } = string.Empty;

    public string Match { get; set; } = string.Empty;

    public string Mode { get; set; } = "contains";
}
