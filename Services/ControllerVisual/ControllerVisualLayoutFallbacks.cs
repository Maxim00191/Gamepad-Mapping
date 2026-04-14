using Gamepad_Mapping.Utils.ControllerVisual;
using GamepadMapperGUI.Models.ControllerVisual;

namespace Gamepad_Mapping.Services.ControllerVisual;

internal static class ControllerVisualLayoutFallbacks
{
    internal static readonly ControllerVisualLayoutDescriptor Xbox = new(
        LayoutKey: "xbox",
        SvgFileName: ControllerSvgConstants.XboxControllerSvgFileName,
        Regions:
        [
            new("shoulder_R", "shoulder_R"),
            new("trigger_R", "trigger_R"),
            new("shoulder_L", "shoulder_L"),
            new("trigger_L", "trigger_L"),
            new("btn_share", "btn_share"),
            new("btn_back", "btn_back"),
            new("btn_home", "btn_home"),
            new("btn_start", "btn_start"),
            new("dpad_U", "dpad_U"),
            new("dpad_D", "dpad_D"),
            new("dpad_L", "dpad_L"),
            new("dpad_R", "dpad_R"),
            new("btn_Y", "btn_Y"),
            new("btn_A", "btn_A"),
            new("btn_X", "btn_X"),
            new("btn_B", "btn_B"),
            new("thumb_L", "thumb_L"),
            new("thumbStick_L", "thumbStick_L"),
            new("thumb_R", "thumb_R"),
            new("thumbStick_R", "thumbStick_R")
        ],
        DisplayName: "Xbox");
}
