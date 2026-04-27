using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using Moq;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphSmokeRunnerTests
{
    [Fact]
    public async Task RunOnceAsync_UsesConnectedProbeOutputForMouseTargeting()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var virtualMouse = new Mock<IVirtualScreenMouse>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .Setup(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 142, 218)));
        virtualMouse
            .Setup(v => v.MoveCursorToVirtualScreenPixels(142, 218));
        mouse.Setup(m => m.LeftClick());

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            virtualMouse.Object,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = "roi",
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 10,
                ["y"] = 12,
                ["width"] = 40,
                ["height"] = 36
            }
        });
        var find = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageNeedlePath] = "needle.png"
        });
        var click = CreateNode("output.mouse_click", new JsonObject
        {
            [AutomationNodePropertyKeys.MouseUseMatchPosition] = true
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, find, click],
            Edges =
            [
                Edge(capture.Id, "flow.out", find.Id, "flow.in"),
                Edge(capture.Id, "screen.image", find.Id, "haystack.image"),
                Edge(find.Id, "flow.out", click.Id, "flow.in"),
                Edge(find.Id, "probe.image", click.Id, "probe.image")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        virtualMouse.Verify(v => v.MoveCursorToVirtualScreenPixels(142, 218), Times.Once);
        mouse.Verify(m => m.LeftClick(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_FailsWhenBranchNodeMissesProbeInput()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen");
        var find = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageNeedlePath] = "needle.png"
        });
        var branch = CreateNode("logic.branch_image");
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, find, branch],
            Edges =
            [
                Edge(capture.Id, "screen.image", find.Id, "haystack.image"),
                Edge(find.Id, "flow.out", branch.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.False(result.Ok);
        Assert.Equal("AutomationSmoke_RunFailed", result.MessageResourceKey);
        Assert.Equal("branch_image:probe_input_missing", result.Detail);
    }

    [Fact]
    public async Task RunOnceAsync_FailsWhenDataGraphContainsCycle()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var findA = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageNeedlePath] = "needle-a.png"
        });
        var findB = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageNeedlePath] = "needle-b.png"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [findA, findB],
            Edges =
            [
                Edge(findA.Id, "probe.image", findB.Id, "haystack.image"),
                Edge(findB.Id, "probe.image", findA.Id, "haystack.image")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.False(result.Ok);
        Assert.Equal("AutomationTopology_DataCycleDetected", result.MessageResourceKey);
    }

    [Fact]
    public async Task RunOnceAsync_ExecutesVariableMathAndBooleanBranch()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var set = CreateNode("variables.set", new JsonObject
        {
            [AutomationNodePropertyKeys.VariableName] = "hp",
            [AutomationNodePropertyKeys.VariableValue] = 15
        });
        var get = CreateNode("variables.get", new JsonObject { [AutomationNodePropertyKeys.VariableName] = "hp" });
        var threshold = CreateNode("math.subtract", new JsonObject { [AutomationNodePropertyKeys.MathRight] = 20 });
        var compare = CreateNode("logic.lt");
        var branch = CreateNode("logic.branch_bool");
        var keyNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [set, get, threshold, compare, branch, keyNode],
            Edges =
            [
                Edge(set.Id, "flow.out", branch.Id, "flow.in"),
                Edge(get.Id, "value", threshold.Id, "left"),
                Edge(threshold.Id, "value", compare.Id, "left"),
                Edge(get.Id, "value", compare.Id, "right"),
                Edge(compare.Id, "value", branch.Id, "condition"),
                Edge(branch.Id, "branch.true", keyNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_FailsWhenBooleanBranchConditionIsMissing()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var branch = CreateNode("logic.branch_bool");
        var doc = new AutomationGraphDocument
        {
            Nodes = [branch]
        };

        var result = await sut.RunOnceAsync(doc);
        Assert.False(result.Ok);
        Assert.Equal("branch_bool:condition_missing", result.Detail);
    }

    [Fact]
    public async Task RunOnceAsync_ExecutesMouseJitterNodeWithConfiguredBaseDelta()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        mouse.Setup(m => m.MoveBy(4, -3));

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var jitter = CreateNode("output.human_noise", new JsonObject
        {
            [AutomationNodePropertyKeys.MouseJitterBaseDeltaX] = 4,
            [AutomationNodePropertyKeys.MouseJitterBaseDeltaY] = -3,
            [AutomationNodePropertyKeys.MouseJitterStickMagnitude] = 0
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [jitter]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        mouse.Verify(m => m.MoveBy(4, -3), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_AppliesHumanNoiseToKeyboardHoldMode()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var noise = new FixedTapHoldNoiseController(adjustedHoldMs: 5);
        keyboard.Setup(k => k.KeyDown(Key.Space));
        keyboard.Setup(k => k.KeyUp(Key.Space));

        var sut = new AutomationGraphSmokeRunner(
            captureService.Object,
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy(),
            null,
            noise);

        var keyNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space",
            [AutomationNodePropertyKeys.KeyboardActionMode] = "hold",
            [AutomationNodePropertyKeys.KeyboardHoldMilliseconds] = 1
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [keyNode]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        Assert.Contains(result.LogLines, line => line.Contains("hold_ms=5", StringComparison.Ordinal));
        keyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);
        keyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    private sealed class FixedTapHoldNoiseController(int adjustedHoldMs) : IHumanInputNoiseController
    {
        public int AdjustDelayMs(int baseDelayMs) => baseDelayMs;

        public int AdjustTapHoldMs(int nominalMs, int maxDeviationMs) => adjustedHoldMs;

        public (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY, float stickMagnitude = 1.0f) =>
            (deltaX, deltaY);
    }

    private static BitmapSource CreateBitmap() =>
        BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);

    private static AutomationNodeState CreateNode(string nodeTypeId, JsonObject? properties = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeTypeId = nodeTypeId,
            Properties = properties
        };

    private static AutomationEdgeState Edge(Guid sourceNodeId, string sourcePortId, Guid targetNodeId, string targetPortId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };
}
