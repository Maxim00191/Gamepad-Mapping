using System.Reflection;
using GamepadMapperGUI.Utils;

namespace GamepadMapping.Tests.Utils;

public sealed class ExceptionMessageFormatterTests
{
    [Fact]
    public void UserFacingMessage_UnwrapsTargetInvocationException()
    {
        var inner = new InvalidOperationException("inner reason");
        var wrapped = new TargetInvocationException(inner);
        Assert.Equal("inner reason", ExceptionMessageFormatter.UserFacingMessage(wrapped));
    }
}
