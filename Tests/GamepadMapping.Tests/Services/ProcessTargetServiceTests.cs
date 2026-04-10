using System;
using System.Text;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ProcessTargetServiceTests
{
    private readonly Mock<IWin32Service> _win32Mock;
    private readonly ProcessTargetService _service;

    public ProcessTargetServiceTests()
    {
        _win32Mock = new Mock<IWin32Service>();
        _service = new ProcessTargetService(_win32Mock.Object);
    }

    [Fact]
    public void CreateTargetFromDeclaredProcessName_WithExeExtension_ReturnsCorrectName()
    {
        // Arrange
        var rawName = "Game.exe";

        // Act
        var result = _service.CreateTargetFromDeclaredProcessName(rawName);

        // Assert
        Assert.Equal("Game", result.ProcessName);
        Assert.Equal(0, result.ProcessId);
    }

    [Fact]
    public void GetForegroundProcessId_ReturnsCorrectPid()
    {
        // Arrange
        var hwnd = new IntPtr(123);
        uint expectedPid = 456;
        _win32Mock.Setup(x => x.GetForegroundWindow()).Returns(hwnd);
        _win32Mock.Setup(x => x.GetWindowThreadProcessId(hwnd, out It.Ref<uint>.IsAny))
            .Callback(new GetWindowThreadProcessIdCallback((IntPtr h, out uint p) => p = expectedPid))
            .Returns(1u);

        // Act
        var result = _service.GetForegroundProcessId();

        // Assert
        Assert.Equal((int)expectedPid, result);
    }

    private delegate void GetWindowThreadProcessIdCallback(IntPtr hWnd, out uint lpdwProcessId);

    [Fact]
    public void IsForeground_ByProcessInfo_MatchesPid()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 456, ProcessName = "Game" };
        var hwnd = new IntPtr(123);
        uint currentPid = 456;
        _win32Mock.Setup(x => x.GetForegroundWindow()).Returns(hwnd);
        _win32Mock.Setup(x => x.GetWindowThreadProcessId(hwnd, out It.Ref<uint>.IsAny))
            .Callback(new GetWindowThreadProcessIdCallback((IntPtr h, out uint p) => p = currentPid))
            .Returns(1u);

        // Act
        var result = _service.IsForeground(target);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsForeground_ByProcessInfo_MatchesWindowTitle()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 0, ProcessName = "Game", MainWindowTitle = "Game Window" };
        var hwnd = new IntPtr(123);
        uint currentPid = 999;
        
        _win32Mock.Setup(x => x.GetForegroundWindow()).Returns(hwnd);
        _win32Mock.Setup(x => x.GetWindowThreadProcessId(hwnd, out It.Ref<uint>.IsAny))
            .Callback(new GetWindowThreadProcessIdCallback((IntPtr h, out uint p) => p = currentPid))
            .Returns(1u);
        
        _win32Mock.Setup(x => x.GetWindowText(hwnd, It.IsAny<StringBuilder>(), It.IsAny<int>()))
            .Returns(new GetWindowTextDelegate((IntPtr h, StringBuilder sb, int max) => 
            {
                sb.Append("Game Window");
                return sb.Length;
            }));

        // Act
        var result = _service.IsForeground(target);

        // Assert
        Assert.True(result);
    }

    private delegate int GetWindowTextDelegate(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [Fact]
    public void IsProcessElevated_ReturnsTrue_WhenTokenIsElevated()
    {
        // Arrange
        int pid = 456;
        var processHandle = new IntPtr(789);
        var tokenHandle = new IntPtr(101);
        
        _win32Mock.Setup(x => x.OpenProcess(It.IsAny<uint>(), false, pid)).Returns(processHandle);
        
        _win32Mock.Setup(x => x.OpenProcessToken(processHandle, It.IsAny<uint>(), out It.Ref<IntPtr>.IsAny))
            .Returns(new OpenProcessTokenDelegate((IntPtr h, uint a, out IntPtr t) => { t = tokenHandle; return true; }));
        
        _win32Mock.Setup(x => x.GetTokenInformation(tokenHandle, 20, It.IsAny<IntPtr>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Returns(new GetTokenInformationDelegate((IntPtr t, int c, IntPtr info, int len, out int ret) => 
            {
                ret = 4;
                System.Runtime.InteropServices.Marshal.WriteInt32(info, 1);
                return true;
            }));

        // Act
        var result = _service.IsProcessElevated(pid);

        // Assert
        Assert.True(result);
        _win32Mock.Verify(x => x.CloseHandle(tokenHandle), Times.Once);
        _win32Mock.Verify(x => x.CloseHandle(processHandle), Times.Once);
    }

    private delegate bool OpenProcessTokenDelegate(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);
    private delegate bool GetTokenInformationDelegate(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);
}


