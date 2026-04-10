using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using Moq;


namespace GamepadMapping.Tests.Core.Input;

public class InputFramePipelineTests
{
    [Fact]
    public void Invoke_ExecutesMiddlewaresInOrder()
    {
        var callOrder = new List<string>();
        var middleware1 = new Mock<IInputFrameMiddleware>();
        middleware1.Setup(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()))
            .Callback<InputFrameContext, Action<InputFrameContext>>((ctx, next) =>
            {
                callOrder.Add("m1");
                next(ctx);
            });

        var middleware2 = new Mock<IInputFrameMiddleware>();
        middleware2.Setup(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()))
            .Callback<InputFrameContext, Action<InputFrameContext>>((ctx, next) =>
            {
                callOrder.Add("m2");
                next(ctx);
            });

        var terminalCalled = false;
        Action<InputFrameContext> terminal = ctx => terminalCalled = true;

        var pipeline = new InputFramePipeline(new[] { middleware1.Object, middleware2.Object }, terminal);
        var context = new InputFrameContext { Frame = new InputFrame() };

        pipeline.Invoke(context);

        Assert.Equal(new[] { "m1", "m2" }, callOrder);
        Assert.True(terminalCalled);
    }

    [Fact]
    public void Invoke_ShortCircuitsWhenNextIsNotCalled()
    {
        var middleware1 = new Mock<IInputFrameMiddleware>();
        middleware1.Setup(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()))
            .Callback<InputFrameContext, Action<InputFrameContext>>((ctx, next) =>
            {
                // Do NOT call next(ctx)
            });

        var middleware2 = new Mock<IInputFrameMiddleware>();
        var terminalCalled = false;
        Action<InputFrameContext> terminal = ctx => terminalCalled = true;

        var pipeline = new InputFramePipeline(new[] { middleware1.Object, middleware2.Object }, terminal);
        var context = new InputFrameContext { Frame = new InputFrame() };

        pipeline.Invoke(context);

        middleware1.Verify(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()), Times.Once);
        middleware2.Verify(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()), Times.Never);
        Assert.False(terminalCalled);
    }

    [Fact]
    public void Invoke_AllowsContextMutation()
    {
        var middleware = new Mock<IInputFrameMiddleware>();
        middleware.Setup(m => m.Invoke(It.IsAny<InputFrameContext>(), It.IsAny<Action<InputFrameContext>>()))
            .Callback<InputFrameContext, Action<InputFrameContext>>((ctx, next) =>
            {
                ctx.IsFirstFrame = true;
                next(ctx);
            });

        var pipeline = new InputFramePipeline(new[] { middleware.Object }, ctx => { });
        var context = new InputFrameContext { Frame = new InputFrame(), IsFirstFrame = false };

        pipeline.Invoke(context);

        Assert.True(context.IsFirstFrame);
    }
}


