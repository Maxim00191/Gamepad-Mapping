using System;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Input;

public sealed class ProfileRuleClipboardService : IProfileRuleClipboardService
{
    private ProfileRuleClipboardEnvelope? _last;

    public void Store(ProfileRuleClipboardEnvelope envelope) =>
        _last = envelope ?? throw new ArgumentNullException(nameof(envelope));

    public bool TryGet(out ProfileRuleClipboardEnvelope? envelope)
    {
        envelope = _last;
        return _last is not null;
    }
}
