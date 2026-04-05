using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

public sealed class NullRadialMenuHud : IRadialMenuHud
{
    public static NullRadialMenuHud Instance { get; } = new();

    private NullRadialMenuHud()
    {
    }

    public void ShowMenu(string title, IReadOnlyList<RadialMenuHudItem> items)
    {
    }

    public void HideMenu()
    {
    }

    public void UpdateSelection(int index)
    {
    }

    public void Dispose()
    {
    }
}
