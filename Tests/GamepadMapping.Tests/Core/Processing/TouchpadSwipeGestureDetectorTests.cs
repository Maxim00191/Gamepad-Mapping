using GamepadMapperGUI.Core.Processing;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core.Processing;

public sealed class TouchpadSwipeGestureDetectorTests
{
    [Fact]
    public void Update_AfterVerticalStrokeUp_ReturnsUpOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.5f, 0.7f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.5f, 0.15f)));

        var dir = sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f));

        Assert.Equal(TouchpadSwipeDirection.Up, dir);
    }

    [Fact]
    public void Update_AfterHorizontalStrokeRight_ReturnsRightOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.2f, 0.5f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.85f, 0.5f)));

        var dir = sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f));

        Assert.Equal(TouchpadSwipeDirection.Right, dir);
    }

    [Fact]
    public void Update_ShortMovement_ReturnsNullOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 3, 0.5f, 0.5f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 3, 0.52f, 0.52f)));

        Assert.Null(sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f)));
    }

    [Fact]
    public void Update_AfterHorizontalStrokeLeft_ReturnsLeftOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.85f, 0.5f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.12f, 0.5f)));

        var dir = sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f));

        Assert.Equal(TouchpadSwipeDirection.Left, dir);
    }

    [Fact]
    public void Update_AfterVerticalStrokeDown_ReturnsDownOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.5f, 0.18f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.5f, 0.92f)));

        var dir = sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f));

        Assert.Equal(TouchpadSwipeDirection.Down, dir);
    }

    [Fact]
    public void Update_DiagonalMovementDominatesNeitherAxis_ReturnsNullOnRelease()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.35f, 0.35f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.62f, 0.62f)));

        Assert.Null(sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f)));
    }

    [Fact]
    public void Update_WhenTrackingIdChanges_RebasesOriginSoLiftDoesNotUsePriorFingerTrajectory()
    {
        var sut = new TouchpadSwipeGestureDetector();

        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.2f, 0.5f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 1, 0.78f, 0.5f)));
        Assert.Null(sut.Update(new PlayStationTouchPoint(true, 2, 0.5f, 0.5f)));

        var dir = sut.Update(new PlayStationTouchPoint(false, 0, 0f, 0f));

        Assert.Null(dir);
    }
}
