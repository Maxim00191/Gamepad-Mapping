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
    public void EvaluateKeyboardTransition_PrecisionJitter_MaintainsStateWithHysteresis()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "RIGHT");
        var source = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: false, DirectionSign: +1);

        // Threshold is 0.35f, Hysteresis is 0.01f
        // 1. Just below threshold -> Inactive
        Assert.False(processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.349f, 0f), Key.W).IsActive);
        
        // 2. Cross threshold -> Active
        var toActive = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.351f, 0f), Key.W);
        Assert.True(toActive.IsActive);
        Assert.True(toActive.HasChanged);
        
        // 3. Jitter back slightly below threshold (0.345) -> Should STAY Active due to hysteresis (threshold 0.34)
        var stayActive = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.345f, 0f), Key.W);
        Assert.True(stayActive.IsActive, "Should stay active due to hysteresis");
        Assert.False(stayActive.HasChanged);

        // 4. Drop below hysteresis threshold (0.339) -> Inactive
        var toInactive = processor.EvaluateKeyboardTransition(mapping, source, new Vector2(0.339f, 0f), Key.W);
        Assert.False(toInactive.IsActive);
        Assert.True(toInactive.HasChanged);
    }

    [Fact]
    public void EvaluateKeyboardTransition_Circularity_CanUseMagnitudeThreshold()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "MAGNITUDE");
        Assert.True(AnalogProcessor.TryParseAnalogSource("MAGNITUDE", out var source));

        // Vector (0.3, 0.3) has magnitude ~0.424, which is > 0.35 threshold.
        var vec = new Vector2(0.3f, 0.3f);
        var result = processor.EvaluateKeyboardTransition(mapping, source, vec, Key.W);
        
        Assert.True(result.IsActive, "Magnitude-based threshold should trigger for (0.3, 0.3)");
    }

    [Fact]
    public void ResolveStickAxisValue_OutOfRange_ClampsToUnitRange()
    {
        var sourceRight = new AnalogSourceDefinition(IsDirectional: true, IsSignedAxis: false, IsVerticalAxis: false, DirectionSign: +1);
        
        // Extreme values should be clamped to [0, 1] for directional
        Assert.Equal(1.0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(1.5f, 0f), sourceRight), 3);
        Assert.Equal(0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(-2.0f, 0f), sourceRight), 3);
        
        // NaN/Infinity should be handled as 0
        Assert.Equal(0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(float.NaN, 0f), sourceRight));
        Assert.Equal(0f, AnalogProcessor.ResolveStickAxisValue(new Vector2(float.PositiveInfinity, 0f), sourceRight));
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

        // First evaluation below custom threshold is inactive and not a change.
        var below = processor.EvaluateTriggerTransition(mapping, 0.69f);
        Assert.False(below.IsActive);
        Assert.False(below.HasChanged);

        // At threshold -> active.
        var at = processor.EvaluateTriggerTransition(mapping, 0.7f);
        Assert.True(at.IsActive);
        Assert.True(at.HasChanged);

        // Above threshold with no state change.
        var above = processor.EvaluateTriggerTransition(mapping, 1.0f);
        Assert.True(above.IsActive);
        Assert.False(above.HasChanged);
    }

    [Fact]
    public void EvaluateTriggerTransition_DefaultThreshold_AppliesWhenAnalogThresholdNull()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftTrigger, "LT", analogThreshold: null);

        // First evaluation slightly below default threshold is inactive and not a change.
        var below = processor.EvaluateTriggerTransition(mapping, 0.34f);
        Assert.False(below.IsActive);
        Assert.False(below.HasChanged);

        // At default threshold -> active.
        var at = processor.EvaluateTriggerTransition(mapping, 0.35f);
        Assert.True(at.IsActive);
        Assert.True(at.HasChanged);
    }

    [Fact]
    public void AccumulateMouseLookDelta_AccumulatesResidualsCorrectly()
    {
        var processor = new AnalogProcessor();
        
        // 0.1f * 10 should result in 1 pixel eventually
        for (int i = 0; i < 9; i++)
        {
            var delta = processor.AccumulateMouseLookDelta(GamepadBindingType.RightThumbstick, 0.1f, 0f);
            Assert.Equal(0, delta.PixelDx);
        }

        var tenthDelta = processor.AccumulateMouseLookDelta(GamepadBindingType.RightThumbstick, 0.1f, 0f);
        Assert.Equal(1, tenthDelta.PixelDx);

        // Reset should clear residuals
        processor.AccumulateMouseLookDelta(GamepadBindingType.RightThumbstick, 0.5f, 0.5f);
        processor.Reset();
        
        var afterReset = processor.AccumulateMouseLookDelta(GamepadBindingType.RightThumbstick, 0.5f, 0.5f);
        Assert.Equal(0, afterReset.PixelDx);
        Assert.Equal(0, afterReset.PixelDy);
    }

    [Theory]
    [InlineData("  x _ PLUS  ", true, true)]
    [InlineData("y-minus", true, true)]
    [InlineData("  HORIZONTAL  ", true, false)]
    [InlineData("  ", false, false)]
    public void TryParseAnalogSource_HandlesMessyStrings(string input, bool expectedSuccess, bool expectedDirectional)
    {
        var success = AnalogProcessor.TryParseAnalogSource(input, out var source);
        Assert.Equal(expectedSuccess, success);
        if (success)
        {
            Assert.Equal(expectedDirectional, source.IsDirectional);
        }
    }

    [Fact]
    public void GetActiveNonTapOutputs_HandlesMalformedStateKeysGracefully()
    {
        var processor = new AnalogProcessor();
        
        // Manually inject some states via Evaluate (since _analogOutputStates is private)
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "RIGHT");
        var source = new AnalogSourceDefinition(true, false, false, +1);
        
        // Valid active state
        processor.EvaluateKeyboardTransition(mapping, source, new Vector2(1.0f, 0f), Key.W);
        
        // Tap trigger should be ignored by GetActiveNonTapOutputs
        var tapMapping = CreateMapping(GamepadBindingType.LeftThumbstick, "RIGHT", TriggerMoment.Tap);
        processor.EvaluateKeyboardTransition(tapMapping, source, new Vector2(1.0f, 0f), Key.T);

        var active = processor.GetActiveNonTapOutputs().ToList();
        
        Assert.Single(active);
        Assert.Equal(Key.W, active[0].Key);
        Assert.Equal(TriggerMoment.Pressed, active[0].Trigger);
    }

    [Fact]
    public void Reset_ClearsAllStates()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftThumbstick, "RIGHT");
        var source = new AnalogSourceDefinition(true, false, false, +1);

        processor.EvaluateKeyboardTransition(mapping, source, new Vector2(1.0f, 0f), Key.W);
        Assert.NotEmpty(processor.GetActiveNonTapOutputs());

        processor.Reset();
        Assert.Empty(processor.GetActiveNonTapOutputs());
    }

    [Fact]
    public void EvaluateTriggerTransition_PrecisionJitter_MaintainsStateWithHysteresis()
    {
        var processor = new AnalogProcessor();
        var mapping = CreateMapping(GamepadBindingType.LeftTrigger, "LT");

        // Threshold 0.35, Hysteresis 0.01
        // 1. Cross threshold -> Active
        Assert.True(processor.EvaluateTriggerTransition(mapping, 0.351f).IsActive);

        // 2. Jitter slightly below threshold (0.345) -> Should STAY Active
        var stayActive = processor.EvaluateTriggerTransition(mapping, 0.345f);
        Assert.True(stayActive.IsActive, "Trigger should stay active due to hysteresis");
        Assert.False(stayActive.HasChanged);

        // 3. Drop below hysteresis threshold (0.339) -> Inactive
        var toInactive = processor.EvaluateTriggerTransition(mapping, 0.339f);
        Assert.False(toInactive.IsActive);
        Assert.True(toInactive.HasChanged);
    }

    [Fact]
    public void StateKey_HandlesPipeInSourceValue_Implicitly()
    {
        var processor = new AnalogProcessor();
        // Create a mapping where From.Value contains a pipe character
        var mapping = new MappingEntry
        {
            From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "MY|AXIS" },
            KeyboardKey = "W",
            Trigger = TriggerMoment.Pressed
        };
        
        var source = new AnalogSourceDefinition(true, false, false, +1);
        processor.EvaluateKeyboardTransition(mapping, source, new Vector2(1.0f, 0f), Key.W);

        var active = processor.GetActiveNonTapOutputs().ToList();
        Assert.Single(active);
        Assert.Equal(Key.W, active[0].Key);
    }
}

