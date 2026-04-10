using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Moq;


namespace GamepadMapping.Tests.Core.Input;

public class InputFrameTransitionMiddlewareTests
{
    [Fact]
    public void Invoke_FirstFrame_SetsIsFirstFrameTrue()
    {
        var middleware = new InputFrameTransitionMiddleware();
        var context = new InputFrameContext { Frame = new InputFrame { Buttons = GamepadButtons.A } };
        bool nextCalled = false;

        middleware.Invoke(context, ctx =>
        {
            nextCalled = true;
            Assert.True(ctx.IsFirstFrame);
            Assert.Equal(GamepadButtons.None, ctx.PreviousButtonsMask);
            Assert.Empty(ctx.PressedButtons);
            Assert.Empty(ctx.ReleasedButtons);
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public void Invoke_SecondFrame_CalculatesTransitions()
    {
        var middleware = new InputFrameTransitionMiddleware();
        
        // First frame
        middleware.Invoke(new InputFrameContext { Frame = new InputFrame { Buttons = GamepadButtons.A } }, _ => { });

        // Second frame: A released, B pressed
        var context = new InputFrameContext { Frame = new InputFrame { Buttons = GamepadButtons.B } };
        bool nextCalled = false;

        middleware.Invoke(context, ctx =>
        {
            nextCalled = true;
            Assert.False(ctx.IsFirstFrame);
            Assert.Equal(GamepadButtons.A, ctx.PreviousButtonsMask);
            Assert.Single(ctx.PressedButtons, GamepadButtons.B);
            Assert.Single(ctx.ReleasedButtons, GamepadButtons.A);
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public void Invoke_MultipleButtons_CalculatesTransitionsCorrectly()
    {
        var middleware = new InputFrameTransitionMiddleware();
        
        // First frame: A down
        middleware.Invoke(new InputFrameContext { Frame = new InputFrame { Buttons = GamepadButtons.A } }, _ => { });

        // Second frame: A still down, X pressed
        var context = new InputFrameContext { Frame = new InputFrame { Buttons = GamepadButtons.A | GamepadButtons.X } };
        
        middleware.Invoke(context, ctx =>
        {
            Assert.Single(ctx.PressedButtons, GamepadButtons.X);
            Assert.Empty(ctx.ReleasedButtons);
        });
    }
}


