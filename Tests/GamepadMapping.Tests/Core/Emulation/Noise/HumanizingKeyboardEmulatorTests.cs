using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;
using System.Windows.Input;
using Xunit;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HumanizingKeyboardEmulatorTests
{
    [Fact]
    public void TapKey_PassesAdjustedHoldToInner()
    {
        var inner = new Mock<IKeyboardEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustTapHoldMs(70, 10)).Returns(77);

        var emu = new HumanizingKeyboardEmulator(inner.Object, noise.Object);
        emu.TapKey(Key.A);

        inner.Verify(k => k.TapKey(Key.A, 1, 0, 77), Times.Once());
    }

    [Fact]
    public void Execute_KeyTap_AdjustsMetadataFromNominal()
    {
        var inner = new Mock<IKeyboardEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustTapHoldMs(50, 10)).Returns(58);

        var emu = new HumanizingKeyboardEmulator(inner.Object, noise.Object);
        emu.Execute(new OutputCommand(OutputCommandType.KeyTap, Key.B, Metadata: 50));

        inner.Verify(
            k => k.Execute(It.Is<OutputCommand>(c =>
                c.Type == OutputCommandType.KeyTap && c.Key == Key.B && c.Metadata == 58)),
            Times.Once());
    }

    [Fact]
    public void Execute_KeyTap_ZeroMetadata_UsesDefaultNominal70()
    {
        var inner = new Mock<IKeyboardEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustTapHoldMs(70, 10)).Returns(72);

        var emu = new HumanizingKeyboardEmulator(inner.Object, noise.Object);
        emu.Execute(new OutputCommand(OutputCommandType.KeyTap, Key.C, Metadata: 0));

        inner.Verify(
            k => k.Execute(It.Is<OutputCommand>(c =>
                c.Type == OutputCommandType.KeyTap && c.Metadata == 72)),
            Times.Once());
    }
}
