#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapping.Tests.Models.Automation;

public sealed class AutomationImageProbeOptionsTests
{
    [Fact]
    public void CombineTemplateMatchMinNormalizedCorrelation_UsesMaxOfInvertedToleranceAndConfidence()
    {
        var combined = AutomationImageProbeOptions.CombineTemplateMatchMinNormalizedCorrelation(0.18, 0.72);
        Assert.Equal(Math.Max(1.0 - 0.18, 0.72), combined, 6);
    }

    [Fact]
    public void ResolveTemplateMatchMinNormalizedCorrelation_PrefersExplicitCombinedField()
    {
        var options = new AutomationImageProbeOptions(0.5, 500, TemplateMatchMinNormalizedCorrelation: 0.88);
        Assert.Equal(0.88, options.ResolveTemplateMatchMinNormalizedCorrelation(), 6);
    }

    [Fact]
    public void ResolveTemplateMatchMinNormalizedCorrelation_FallsBackToToleranceWhenCombinedNotSet()
    {
        var options = new AutomationImageProbeOptions(0.2, 500);
        Assert.Equal(0.8, options.ResolveTemplateMatchMinNormalizedCorrelation(), 6);
    }
}
