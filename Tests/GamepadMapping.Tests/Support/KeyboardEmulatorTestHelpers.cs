using System.Windows.Input;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;

namespace GamepadMapping.Tests.Support;

internal static class KeyboardEmulatorTestHelpers
{
    public static void VerifyExecuteKeyPress(Mock<IKeyboardEmulator> mock, Key key, Moq.Times expected) =>
        mock.Verify(
            k => k.ExecuteAsync(
                It.Is<OutputCommand>(c => c.Type == OutputCommandType.KeyPress && c.Key == key),
                It.IsAny<CancellationToken>()),
            expected);

    public static void VerifyExecuteKeyRelease(Mock<IKeyboardEmulator> mock, Key key, Moq.Times expected) =>
        mock.Verify(
            k => k.ExecuteAsync(
                It.Is<OutputCommand>(c => c.Type == OutputCommandType.KeyRelease && c.Key == key),
                It.IsAny<CancellationToken>()),
            expected);

    public static void VerifyExecuteKeyTap(Mock<IKeyboardEmulator> mock, Key key, Moq.Times expected) =>
        mock.Verify(
            k => k.ExecuteAsync(
                It.Is<OutputCommand>(c => c.Type == OutputCommandType.KeyTap && c.Key == key),
                It.IsAny<CancellationToken>()),
            expected);

    public static void VerifyExecuteKeyPress(Mock<IKeyboardEmulator> mock, Key key, Moq.Times expected, string failMessage) =>
        mock.Verify(
            k => k.ExecuteAsync(
                It.Is<OutputCommand>(c => c.Type == OutputCommandType.KeyPress && c.Key == key),
                It.IsAny<CancellationToken>()),
            expected,
            failMessage);

    public static void VerifyExecuteKeyTap(Mock<IKeyboardEmulator> mock, Key key, Moq.Times expected, string failMessage) =>
        mock.Verify(
            k => k.ExecuteAsync(
                It.Is<OutputCommand>(c => c.Type == OutputCommandType.KeyTap && c.Key == key),
                It.IsAny<CancellationToken>()),
            expected,
            failMessage);
}
