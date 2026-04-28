using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using Moq;

namespace GamepadMapping.Tests.Services.Input;

public class GamepadSourceFactoryTests
{
    [Fact]
    public void NormalizeApiId_UnknownValue_FallsBackToXInput()
    {
        var factory = CreateFactory();

        var normalized = factory.NormalizeApiId("UnknownBackend");

        Assert.Equal(GamepadSourceApiIds.XInput, normalized);
    }

    [Fact]
    public void CreateSource_DualSenseRequested_FallsBackToXInputSource()
    {
        var factory = CreateFactory();

        var source = factory.CreateSource(GamepadSourceApiIds.DualSense, out var resolvedApiId);

        Assert.IsType<XInputSource>(source);
        Assert.Equal(GamepadSourceApiIds.XInput, resolvedApiId);
    }

    [Fact]
    public void GetRegistrations_IncludesDualSenseForFutureExpansion()
    {
        var factory = CreateFactory();

        var registrations = factory.GetRegistrations();

        Assert.Contains(registrations, r => r.Id == GamepadSourceApiIds.XInput && r.IsImplemented);
        Assert.Contains(registrations, r => r.Id == GamepadSourceApiIds.DualSense && !r.IsImplemented);
    }

    private static GamepadSourceFactory CreateFactory()
    {
        var xInputMock = new Mock<IXInput>();
        return new GamepadSourceFactory(xInputMock.Object);
    }
}
