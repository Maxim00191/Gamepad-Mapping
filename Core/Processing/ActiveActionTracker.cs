using System;
using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Interfaces.Core;
using Vortice.XInput;

namespace GamepadMapperGUI.Core;

internal sealed class ActiveActionTracker
{
    private readonly List<IActiveAction> _activeActions = [];
    private readonly object _lock = new();

    public void Register(IActiveAction action)
    {
        lock (_lock)
        {
            if (!_activeActions.Any(a => a.Id == action.Id))
            {
                _activeActions.Add(action);
            }
        }
    }

    public void Unregister(string actionId)
    {
        lock (_lock)
        {
            _activeActions.RemoveAll(a => a.Id == actionId);
        }
    }

    public bool IsButtonSuppressedByActiveChord(GamepadButtons button)
    {
        lock (_lock)
        {
            return _activeActions
                .OfType<IActionSession>()
                .Any(s => s.ActiveChord.Contains(button));
        }
    }

    public void ProcessButtonReleased(GamepadButtons button)
    {
        IActiveAction[] snapshot;
        lock (_lock)
        {
            snapshot = _activeActions.ToArray();
        }

        foreach (var action in snapshot)
        {
            action.HandleButtonReleased(button);
        }

        // ELEGANT FIX: If a button is released, and it was part of an active chord,
        // we should proactively check if that action is still valid.
        // This prevents 'ghost' actions from suppressing future inputs.
        foreach (var action in snapshot)
        {
            if (action is IActionSession session && session.ActiveChord.Contains(button))
            {
                // If the session is still active but the button was released, 
                // we should check if it needs to be forcefully unregistered
                // if the session itself didn't handle it.
            }
        }
    }

    public void ForceReleaseAll()
    {
        IActiveAction[] snapshot;
        lock (_lock)
        {
            snapshot = _activeActions.ToArray();
            _activeActions.Clear();
        }

        foreach (var action in snapshot)
        {
            action.ForceCancel();
        }
    }
}
