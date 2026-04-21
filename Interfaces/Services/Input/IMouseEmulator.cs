using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Injected mouse button/wheel/move simulation (implementation may use Win32, etc.).
/// </summary>
/// <remarks>
/// Synchronous <c>*Click</c> methods block for the emulated button-hold interval. Call them only from a worker or dedicated emulation thread, not from the UI thread. Prefer <c>*Async</c> when integrating with async dispatch (e.g. output queue workers).
/// </remarks>
public interface IMouseEmulator
{
    void Execute(OutputCommand command);
    Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default);

    void LeftDown();
    void LeftUp();
    void LeftClick();
    Task LeftClickAsync(CancellationToken cancellationToken = default);
    void RightDown();
    void RightUp();
    void RightClick();
    Task RightClickAsync(CancellationToken cancellationToken = default);
    void MiddleDown();
    void MiddleUp();
    void MiddleClick();
    Task MiddleClickAsync(CancellationToken cancellationToken = default);
    void X1Down();
    void X1Up();
    void X1Click();
    Task X1ClickAsync(CancellationToken cancellationToken = default);
    void X2Down();
    void X2Up();
    void X2Click();
    Task X2ClickAsync(CancellationToken cancellationToken = default);
    void WheelUp();
    void WheelDown();
    /// <param name="moveSubdivisionScope">
    /// For mouse-look from a thumbstick, pass <see cref="GamepadBindingType.LeftThumbstick"/> or <see cref="GamepadBindingType.RightThumbstick"/> so layered emulators can track per-stick pending move subdivision. Other callers may omit.
    /// </param>
    void MoveBy(int deltaX, int deltaY, float stickMagnitude = 1.0f, GamepadBindingType? moveSubdivisionScope = null);
}

