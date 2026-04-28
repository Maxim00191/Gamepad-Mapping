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
    public void NormalizeApiId_WithWhitespaceAndCase_ResolvesToCanonicalPlayStationId()
    {
        var factory = CreateFactory();

        var normalized = factory.NormalizeApiId("  playstation  ");

        Assert.Equal(GamepadSourceApiIds.PlayStation, normalized);
    }

    [Fact]
    public void CreateSource_PlayStationRequested_ReturnsPlayStationNativeSource()
    {
        var factory = CreateFactory();

        var source = factory.CreateSource(GamepadSourceApiIds.PlayStation, out var resolvedApiId);

        Assert.IsType<PlayStationNativeSource>(source);
        Assert.Equal(GamepadSourceApiIds.PlayStation, resolvedApiId);
    }

    [Fact]
    public void NormalizeApiId_LegacyDualSenseAlias_MapsToPlayStation()
    {
        var factory = CreateFactory();

        var normalized = factory.NormalizeApiId(GamepadSourceApiIds.DualSense);

        Assert.Equal(GamepadSourceApiIds.PlayStation, normalized);
    }

    [Fact]
    public void GetRegistrations_IncludesPlayStationAsImplemented()
    {
        var factory = CreateFactory();

        var registrations = factory.GetRegistrations();

        Assert.Contains(registrations, r => r.Id == GamepadSourceApiIds.XInput && r.IsImplemented);
        Assert.Contains(registrations, r => r.Id == GamepadSourceApiIds.PlayStation && r.IsImplemented);
    }

    private static GamepadSourceFactory CreateFactory()
    {
        var xInputMock = new Mock<IXInput>();
        var playStationProviderMock = new Mock<IPlayStationInputProvider>();
        return new GamepadSourceFactory(xInputMock.Object, playStationProviderMock.Object);
    }
}
