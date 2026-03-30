namespace GamepadMapperGUI.Interfaces.Core;

public interface IMouseEmulator
{
    void LeftDown();
    void LeftUp();
    void LeftClick();
    void RightDown();
    void RightUp();
    void RightClick();
    void MiddleDown();
    void MiddleUp();
    void MiddleClick();
    void X1Down();
    void X1Up();
    void X1Click();
    void X2Down();
    void X2Up();
    void X2Click();
    void WheelUp();
    void WheelDown();
    void MoveBy(int deltaX, int deltaY);
}
