using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Radial;

public interface IRadialMenuHud : IDisposable
{
    void ShowMenu(string title, IReadOnlyList<RadialMenuHudItem> items);
    void HideMenu();
    void UpdateSelection(int index);
}

