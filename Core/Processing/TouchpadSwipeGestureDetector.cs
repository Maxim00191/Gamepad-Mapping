using System;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Core.Processing;

internal sealed class TouchpadSwipeGestureDetector
{
    private bool _hadContact;
    private int _trackingId;
    private float _originX;
    private float _originY;
    private float _lastX;
    private float _lastY;

    public TouchpadSwipeDirection? Update(in PlayStationTouchPoint primary)
    {
        if (_hadContact && !primary.IsActive)
        {
            var completed = TryClassify(_originX, _originY, _lastX, _lastY);
            _hadContact = false;
            return completed;
        }

        if (!primary.IsActive)
        {
            _hadContact = false;
            return null;
        }

        if (!_hadContact || primary.TrackingId != _trackingId)
        {
            _originX = primary.XNormalized;
            _originY = primary.YNormalized;
            _trackingId = primary.TrackingId;
            _hadContact = true;
        }

        _lastX = primary.XNormalized;
        _lastY = primary.YNormalized;
        return null;
    }

    public void Reset() => _hadContact = false;

    private static TouchpadSwipeDirection? TryClassify(float ox, float oy, float lx, float ly)
    {
        var dx = lx - ox;
        var dy = ly - oy;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < TouchpadGestureConstraints.MinSwipeDistanceNormalized)
            return null;

        var absX = MathF.Abs(dx);
        var absY = MathF.Abs(dy);
        var ratio = TouchpadGestureConstraints.MinDominantAxisRatio;
        var min = TouchpadGestureConstraints.MinSwipeDistanceNormalized;

        if (absY >= absX * ratio && absY >= min)
            return dy < 0f ? TouchpadSwipeDirection.Up : TouchpadSwipeDirection.Down;

        if (absX >= absY * ratio && absX >= min)
            return dx < 0f ? TouchpadSwipeDirection.Left : TouchpadSwipeDirection.Right;

        return null;
    }
}
