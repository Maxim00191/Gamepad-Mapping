using System.Threading;
using System.Threading.Tasks;
using GamepadMapperGUI.Interfaces.Services.Input;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>Preserves legacy synchronous sub-move emission (used in unit tests and as a fallback).</summary>
public sealed class ImmediateMouseSubMoveStepDelayer : IMouseSubMoveStepDelayer
{
    public static ImmediateMouseSubMoveStepDelayer Instance { get; } = new();

    private ImmediateMouseSubMoveStepDelayer() { }

    public IMouseSubMoveScheduleSession BeginScheduleSession(int stepsInThisBatch, CancellationToken cancellationToken) =>
        ImmediateSession.Instance;

    private sealed class ImmediateSession : IMouseSubMoveScheduleSession
    {
        internal static readonly ImmediateSession Instance = new();

        public ValueTask DelayBeforeNextSubMoveAsync(CancellationToken cancellationToken = default) => default;
    }
}
