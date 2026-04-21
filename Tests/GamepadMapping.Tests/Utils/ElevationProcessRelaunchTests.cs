using Gamepad_Mapping.Utils;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class ElevationProcessRelaunchTests
{
    [Fact]
    public void BuildRelaunchArguments_EmptyOrSingleArg_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ElevationProcessRelaunch.BuildRelaunchArguments(Array.Empty<string>()));
        Assert.Equal(string.Empty, ElevationProcessRelaunch.BuildRelaunchArguments(new[] { @"C:\app\Gamepad Mapping.exe" }));
    }

    [Fact]
    public void BuildRelaunchArguments_PassesThroughUnquotedWhenNoSpaces()
    {
        var args = new[] { @"C:\app\exe", "foo", "bar" };
        Assert.Equal("foo bar", ElevationProcessRelaunch.BuildRelaunchArguments(args));
    }

    [Fact]
    public void BuildRelaunchArguments_QuotesArgsWithSpaces()
    {
        var args = new[] { @"C:\app\Gamepad Mapping.exe", @"F:\Tools\Gamepad Mapping.dll" };
        Assert.Equal("\"F:\\Tools\\Gamepad Mapping.dll\"", ElevationProcessRelaunch.BuildRelaunchArguments(args));
    }
}
