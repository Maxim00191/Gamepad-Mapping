using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class GamepadServiceTests
{
    [Fact]
    public void ApplyInputStreamTuning_ClampsValuesOntoReader()
    {
        var reader = new Mock<IGamepadReader>();
        reader.SetupProperty(r => r.PollingIntervalMs, GamepadInputStreamConstraints.MinPollingIntervalMs);
        reader.SetupProperty(r => r.AnalogChangeEpsilon, GamepadInputStreamConstraints.MinAnalogChangeEpsilon);

        var svc = new GamepadService(reader.Object);
        svc.ApplyInputStreamTuning(999, 0.5f);

        Assert.Equal(GamepadInputStreamConstraints.MaxPollingIntervalMs, reader.Object.PollingIntervalMs);
        Assert.Equal(GamepadInputStreamConstraints.MaxAnalogChangeEpsilon, reader.Object.AnalogChangeEpsilon);
    }
}
