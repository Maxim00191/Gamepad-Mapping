using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>Lets mapping logic drop pending split-move remainder when a thumbstick's mouse-look gesture ends.</summary>
internal interface IPendingMouseSubdivisionState
{
    /// <param name="thumbstickScope">When set, clears carry only for that stick; when null, clears all (e.g. global reset).</param>
    void ClearPendingSubdivision(GamepadBindingType? thumbstickScope = null);
}
