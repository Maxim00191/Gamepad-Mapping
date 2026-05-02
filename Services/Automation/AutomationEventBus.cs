#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationEventBus : IAutomationEventBus
{
    private event Action<string>? EventPublished;

    public void Publish(string signal)
    {
        if (string.IsNullOrWhiteSpace(signal))
            return;

        EventPublished?.Invoke(signal.Trim());
    }

    public void Subscribe(Action<string> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        EventPublished += listener;
    }
}
