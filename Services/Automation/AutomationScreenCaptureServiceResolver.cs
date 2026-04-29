#nullable enable

using System.Text.Json.Nodes;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationScreenCaptureServiceResolver : IAutomationScreenCaptureServiceResolver
{
    private readonly IReadOnlyDictionary<string, IAutomationScreenCaptureService> _backends;
    private readonly string _defaultApiId;

    public AutomationScreenCaptureServiceResolver(
        IReadOnlyDictionary<string, IAutomationScreenCaptureService> backends,
        string defaultApiId)
    {
        _backends = backends ?? throw new ArgumentNullException(nameof(backends));
        _defaultApiId = string.IsNullOrWhiteSpace(defaultApiId)
            ? AutomationCaptureApi.Gdi
            : defaultApiId.Trim();
    }

    public IAutomationScreenCaptureService Resolve(string? captureApiId)
    {
        var key = AutomationCaptureApi.Normalize(captureApiId);
        if (_backends.TryGetValue(key, out var svc))
            return svc;
        if (_backends.TryGetValue(_defaultApiId, out var fallback))
            return fallback;
        throw new InvalidOperationException("automation_capture_backend_missing");
    }

    public IAutomationScreenCaptureService ResolveForNodeProperties(JsonObject? nodeProperties)
    {
        var api = AutomationNodePropertyReader.ReadString(nodeProperties, AutomationNodePropertyKeys.CaptureApi);
        return Resolve(api);
    }
}
