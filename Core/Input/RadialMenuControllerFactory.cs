using System;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core;

internal static class RadialMenuControllerFactory
{
    public static IRadialMenuController Create(
        IRadialMenuHud radialMenuHud,
        IUiSynchronization ui,
        Action<string> setMappedOutput,
        Action<string> setMappingStatus,
        Action<string, TriggerMoment, DispatchedOutput, string, string> enqueueOutput,
        Func<RadialMenuConfirmMode> getConfirmMode,
        Action<IActiveAction> registerActiveAction,
        Action<string> unregisterActiveAction,
        Action<string>? requestTemplateSwitch,
        IKeyboardActionCatalog? catalog) =>
        new RadialMenuController(
            radialMenuHud,
            ui,
            setMappedOutput,
            setMappingStatus,
            enqueueOutput,
            getConfirmMode,
            registerActiveAction,
            unregisterActiveAction,
            requestTemplateSwitch,
            catalog);
}
