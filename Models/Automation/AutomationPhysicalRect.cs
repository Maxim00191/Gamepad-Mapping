namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationPhysicalRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
