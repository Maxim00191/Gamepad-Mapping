using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>In-memory buffer for copy/paste of a single profile rule between templates (not the system clipboard).</summary>
public interface IProfileRuleClipboardService
{
    void Store(ProfileRuleClipboardEnvelope envelope);

    bool TryGet(out ProfileRuleClipboardEnvelope? envelope);
}
