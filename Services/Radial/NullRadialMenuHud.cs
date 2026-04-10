using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services.Radial;

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


