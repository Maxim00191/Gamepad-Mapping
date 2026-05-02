#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationGraphClipboardService
{
    bool TryBuildSelectionPayload(AutomationGraphDocument document, IReadOnlyCollection<Guid> selectedNodeIds, out string payloadText);

    bool TryParsePayloadForPaste(string payloadText, double anchorLogicalX, double anchorLogicalY, out AutomationGraphDocument fragment);
}
