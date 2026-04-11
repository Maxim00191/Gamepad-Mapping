using System;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;

namespace GamepadMapping.Tests.Support;

/// <summary>Runs UI callbacks on the caller thread (tests and synchronous harnesses).</summary>
internal sealed class ImmediateUiSynchronization : IUiSynchronization
{
    public static readonly ImmediateUiSynchronization Instance = new();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public void Send(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
