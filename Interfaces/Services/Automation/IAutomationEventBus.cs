namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationEventBus
{
    void Publish(string signal);

    void Subscribe(Action<string> listener);
}
