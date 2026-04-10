using System.Diagnostics;
using GamepadMapperGUI.Services;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class Win32ServiceTests
{
    private readonly Win32Service _service;

    public Win32ServiceTests()
    {
        _service = new Win32Service();
    }

    [Fact]
    public void GetProcessName_CurrentProcess_ReturnsCorrectName()
    {
        // Arrange
        int pid = Environment.ProcessId;
        string expectedName = Process.GetCurrentProcess().ProcessName;

        // Act
        string actualName = ((GamepadMapperGUI.Interfaces.Services.IWin32Service)_service).GetProcessName(pid);

        // Assert
        Assert.Equal(expectedName, actualName);
    }

    [Fact]
    public void GetProcessName_InvalidPid_ReturnsEmptyString()
    {
        // Act
        string actualName = ((GamepadMapperGUI.Interfaces.Services.IWin32Service)_service).GetProcessName(-1);

        // Assert
        Assert.Equal(string.Empty, actualName);
    }
}
