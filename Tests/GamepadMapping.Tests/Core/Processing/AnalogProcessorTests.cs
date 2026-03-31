using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing;

public class AnalogProcessorTests
{
    private static MappingEntry CreateMapping(
        GamepadBindingType type,
        string fromValue,
        TriggerMoment trigger = TriggerMoment.Pressed,
        float? analogThreshold = null) =>
        new()
        {
            From = new GamepadBinding { Type = type, Value = fromValue },
            KeyboardKey = "W",
            Trigger = trigger,
            AnalogThreshold = analogThreshold
        };

    [Fact]
    public void EvaluateKeyboardTransition_DefaultDeadzone_ActivatesAtOrAboveThreshold()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "RIGHT");
        var source = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: false, DirectionSign: +1);

        // First evaluation below default threshold (0.35) is inactive and does not count as a change.
        var below = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.34f, 0f), Key.W);
        Assert.False(below.IsActive);
        Assert.False(below.HasChanged);

        // At default threshold should be considered active.
        var at = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.35f, 0f), Key.W);
        Assert.True(at.IsActive);
        Assert.True(at.HasChanged);

        // Above threshold, no state change (still active).
        var above = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(1f, 0f), Key.W);
        Assert.True(above.IsActive);
        Assert.False(above.HasChanged);
    }

    [Fact]
    public void ResolveStickAxisValue_DirectionalAxis_UsesPositiveHalfOnly()
    {
        var sourceRight = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: false, DirectionSign: +1);
        var sourceLeft = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: false, DirectionSign: -1);

        // Right direction maps positive X, clamps negatives to 0.
        Assert.Equal(0.8f, AnalogProcessor.ResolveStickAxisValue(new Vector2(0.8f, 0f), sourceRight), 3);
        Assert.Equal(0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(-0.8f, 0f), sourceRight), 3);

        // Left direction maps negative X as positive magnitude, clamps positives to 0.
        Assert.Equal(0.8f, AnalogProcessor.ResolveStickAxisValue(new Vector2(-0.8f, 0f), sourceLeft), 3);
        Assert.Equal(0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(0.8f, 0f), sourceLeft), 3);
    }

    [Fact]
    public void EvaluateKeyboardTransition_FullDeflection_TriggersForDirectionalStick()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "FORWARD");
        var source = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: true, DirectionSign: +1);

        var transition = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0f, 1f), Key.W);

        Assert.True(transition.IsActive);
        Assert.True(transition.HasChanged);
    }

    [Fact]
    public void EvaluateTriggerTransition_UsesAnalogThresholdOverride()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftTrigger, "LT", analogThreshold: 0.7f);
        const string stateKey = "LeftTrigger|LT|W|Pressed";

        // First evaluation below custom threshold is inactive and not a change.
        var below = processor.EvaluateTriggerTransition(mapping, 0.69f, stateKey);
        Assert.False(below.IsActive);
        Assert.False(below.HasChanged);

        // At threshold -> active.
        var at = processor.EvaluateTriggerTransition(mapping, 0.7f, stateKey);
        Assert.True(at.IsActive);
        Assert.True(at.HasChanged);

        // Above threshold with no state change.
        var above = processor.EvaluateTriggerTransition(mapping, 1.0f, stateKey);
        Assert.True(above.IsActive);
        Assert.False(above.HasChanged);
    }

    [Fact]
    public void EvaluateTriggerTransition_DefaultThreshold_AppliesWhenAnalogThresholdNull()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftTrigger, "LT", analogThreshold: null);
        const string stateKey = "LeftTrigger|LT|W|Pressed";

        // First evaluation slightly below default threshold is inactive and not a change.
        var below = processor.EvaluateTriggerTransition(mapping, 0.34f, stateKey);
        Assert.False(below.IsActive);
        Assert.False(below.HasChanged);

        // At default threshold -> active.
        var at = processor.EvaluateTriggerTransition(mapping, 0.35f, stateKey);
        Assert.True(at.IsActive);
        Assert.True(at.HasChanged);
    }
}

