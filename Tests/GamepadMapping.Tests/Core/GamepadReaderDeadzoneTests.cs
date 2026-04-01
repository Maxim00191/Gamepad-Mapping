using GamepadMapperGUI.Core;
using Xunit;

namespace GamepadMapping.Tests.Core;

public class GamepadReaderDeadzoneTests
{
    [Theory]
    [InlineData(0.1f, 0.1f)]   // Normal
    [InlineData(-0.1f, 0.0f)]  // Too low
    [InlineData(0.95f, 0.9f)]  // Too high
    public void LeftThumbstickDeadzone_ClampsValue(float input, float expected)
    {
        var reader = new GamepadReader();
        reader.LeftThumbstickDeadzone = input;
        Assert.Equal(expected, reader.LeftThumbstickDeadzone);
    }

    [Theory]
    [InlineData(0.5f, 0.8f, 0.5f, 0.8f)]   // Normal
    [InlineData(0.5f, 0.4f, 0.5f, 0.52f)]  // Outer too low, should be clamped to inner + 0.02
    [InlineData(0.99f, 1.0f, 0.98f, 1.0f)] // Inner too high, should be clamped to 0.98
    [InlineData(0.1f, 0.05f, 0.1f, 0.12f)] // Outer < inner + span
    public void TriggerDeadzone_EnforcesSpan(float inner, float outer, float expectedInner, float expectedOuter)
    {
        var reader = new GamepadReader();
        
        // Test setting inner then outer
        reader.LeftTriggerInnerDeadzone = inner;
        reader.LeftTriggerOuterDeadzone = outer;
        
        Assert.Equal(expectedInner, reader.LeftTriggerInnerDeadzone, 3);
        Assert.Equal(expectedOuter, reader.LeftTriggerOuterDeadzone, 3);

        // Test setting outer then inner
        reader.RightTriggerOuterDeadzone = outer;
        reader.RightTriggerInnerDeadzone = inner;

        Assert.Equal(expectedInner, reader.RightTriggerInnerDeadzone, 3);
        Assert.Equal(expectedOuter, reader.RightTriggerOuterDeadzone, 3);
    }

    [Fact]
    public void LeftTriggerInnerDeadzone_IncreasesOuterIfNecessary()
    {
        var reader = new GamepadReader();
        reader.LeftTriggerOuterDeadzone = 0.5f;
        reader.LeftTriggerInnerDeadzone = 0.6f; // This should push outer to 0.62
        
        Assert.Equal(0.6f, reader.LeftTriggerInnerDeadzone);
        Assert.Equal(0.62f, reader.LeftTriggerOuterDeadzone);
    }
}
