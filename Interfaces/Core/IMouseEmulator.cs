using System.Threading;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Core;

public interface IMouseEmulator
{
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
    void MoveBy(int deltaX, int deltaY);
}
