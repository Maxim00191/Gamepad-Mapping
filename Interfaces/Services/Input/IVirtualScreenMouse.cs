namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Moves the cursor to physical virtual-screen pixels (same space as Win32 SM_*VIRTUALSCREEN and automation ROI storage).
/// </summary>
public interface IVirtualScreenMouse
{
    void MoveCursorToVirtualScreenPixels(int physicalX, int physicalY);
}
