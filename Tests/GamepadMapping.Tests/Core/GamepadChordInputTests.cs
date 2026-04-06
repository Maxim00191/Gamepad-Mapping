using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core;

public sealed class GamepadChordInputTests
{
    [Theory]
    [InlineData("RightTrigger+B", "RightTrigger + B")]
    [InlineData("RightTrigger + B", "RightTrigger + B")]
    [InlineData("B+RT", "RightTrigger + B")]
    public void TryNormalizeButtonExpression_NormalizesTriggerChords(string raw, string expectedNormalized)
    {
        Assert.True(GamepadChordInput.TryNormalizeButtonExpression(raw, out var normalized));
        Assert.Equal(expectedNormalized, normalized);
    }

    [Fact]
    public void SplitNormalizedParts_SplitsOnSeparator()
    {
        var parts = GamepadChordInput.SplitNormalizedParts("RightTrigger + B");
        Assert.Equal(["RightTrigger", "B"], parts);
    }

    [Theory]
    [InlineData("RightTrigger + B", true)]
    [InlineData("A+B", false)]
    [InlineData("LT+X", true)]
    public void ExpressionInvolvesTrigger_MatchesChordResolver(string raw, bool expected)
    {
        Assert.Equal(expected, GamepadChordInput.ExpressionInvolvesTrigger(raw));
    }

    [Theory]
    [InlineData("RightTrigger", true)]
    [InlineData("LT", true)]
    [InlineData("RightTrigger+B", true)]
    [InlineData("A", false)]
    public void ShouldShowTriggerMatchThresholdEditor_IncludesIncompleteTriggerSelection(string raw, bool expected)
    {
        Assert.Equal(expected, GamepadChordInput.ShouldShowTriggerMatchThresholdEditor(raw));
    }

    [Theory]
    [InlineData("0.35", 0.35f)]
    [InlineData(" 0.5 ", 0.5f)]
    [InlineData("1", 1f)]
    public void TryParseTriggerMatchThreshold_AcceptsOpenIntervalZeroToOneInclusive(string text, float expected)
    {
        Assert.True(GamepadChordInput.TryParseTriggerMatchThreshold(text, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-0.1")]
    [InlineData("1.01")]
    [InlineData("x")]
    public void TryParseTriggerMatchThreshold_RejectsInvalid(string text)
    {
        Assert.False(GamepadChordInput.TryParseTriggerMatchThreshold(text, out _));
    }

    [Fact]
    public void TryCreateNativeTriggerOnlyBinding_LeftTrigger_MatchesJsonTemplateShape()
    {
        Assert.True(GamepadChordInput.TryCreateNativeTriggerOnlyBinding("LeftTrigger", out var b));
        Assert.Equal(GamepadBindingType.LeftTrigger, b.Type);
        Assert.Equal(nameof(GamepadBindingType.LeftTrigger), b.Value);
    }
}
