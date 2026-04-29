#nullable enable

using System.Text.Json.Nodes;

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationScreenCaptureServiceResolver
{
    IAutomationScreenCaptureService Resolve(string? captureApiId);

    IAutomationScreenCaptureService ResolveForNodeProperties(JsonObject? nodeProperties);
}
