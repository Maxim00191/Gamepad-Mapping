using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Utils;
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
            CreateCaptureResolver(captureService.Object),
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
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

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        virtualMouse.Verify(v => v.MoveCursorToVirtualScreenPixels(142, 218), Times.Once);
        mouse.Verify(m => m.LeftClick(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_ChainedFindImage_UsesUpstreamFindHaystackPassthrough()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 5, 6)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 7, 8)));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
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
        var find1 = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var find2 = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var delay = CreateNode("automation.delay", new JsonObject
        {
            [AutomationNodePropertyKeys.DelayMilliseconds] = 1
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, find1, find2, delay],
            Edges =
            [
                Edge(capture.Id, AutomationPortIds.FlowOut, find1.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.ScreenImage, find1.Id, AutomationPortIds.HaystackImage),
                Edge(find1.Id, AutomationPortIds.FlowOut, find2.Id, AutomationPortIds.FlowIn),
                Edge(find1.Id, AutomationPortIds.ProbeImage, find2.Id, AutomationPortIds.HaystackImage),
                Edge(find2.Id, AutomationPortIds.FlowOut, delay.Id, AutomationPortIds.FlowIn)
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        probeService.Verify(
            p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunOnceAsync_CaptureScreenUsesProcessWindowSourceWhenConfigured()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var processCapture = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(50, 60, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService
            .Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "obs64" && target.ProcessId == 0)))
            .Returns(processCapture);

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "obs64"
        });
        var log = CreateNode("debug.log");
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, log],
            Edges =
            [
                Edge(capture.Id, "flow.out", log.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        captureService.Verify(s => s.CaptureProcessWindowPhysical(
            It.Is<AutomationProcessWindowTarget>(target =>
                target.ProcessName == "obs64" && target.ProcessId == 0)), Times.Once);
        captureService.Verify(s => s.CaptureVirtualScreenPhysical(), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_ReevaluatesDataOutputsOnEachLoopIteration()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 1, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 10, 12)));
        keyboard.Setup(k => k.TapKey(Key.A, 1, 0, 70));
        keyboard.Setup(k => k.TapKey(Key.D, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2
        });
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var compare = CreateNode("logic.lt", new JsonObject
        {
            [AutomationNodePropertyKeys.CompareRight] = 5
        });
        var branch = CreateNode("logic.branch_bool");
        var keyA = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A"
        });
        var keyD = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "D"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, loop, capture, find, compare, branch, keyA, keyD],
            Edges =
            [
                Edge(listener.Id, "flow.out", loop.Id, "flow.in"),
                Edge(loop.Id, "loop.body", capture.Id, "flow.in"),
                Edge(capture.Id, "flow.out", find.Id, "flow.in"),
                Edge(capture.Id, "screen.image", find.Id, "haystack.image"),
                Edge(find.Id, "flow.out", branch.Id, "flow.in"),
                Edge(find.Id, "result.x", compare.Id, "left"),
                Edge(compare.Id, "value", branch.Id, "condition"),
                Edge(branch.Id, "branch.true", keyA.Id, "flow.in"),
                Edge(branch.Id, "branch.false", keyD.Id, "flow.in"),
                Edge(keyA.Id, "flow.out", loop.Id, "flow.in"),
                Edge(keyD.Id, "flow.out", loop.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.A, 1, 0, 70), Times.Once);
        keyboard.Verify(k => k.TapKey(Key.D, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_BranchCompare_RoutesExecutionByNumericComparison()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
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
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 8, 12)));
        keyboard.Setup(k => k.TapKey(Key.D, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var branch = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.GreaterThan,
            [AutomationNodePropertyKeys.CompareRight] = 5
        });
        var keyD = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "D"
        });

        var doc = new AutomationGraphDocument
        {
            VerboseExecutionLogging = true,
            Nodes = [capture, find, branch, keyD],
            Edges =
            [
                Edge(capture.Id, AutomationPortIds.FlowOut, find.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.ScreenImage, find.Id, AutomationPortIds.HaystackImage),
                Edge(find.Id, AutomationPortIds.FlowOut, branch.Id, AutomationPortIds.FlowIn),
                Edge(find.Id, "result.x", branch.Id, "left"),
                Edge(branch.Id, AutomationPortIds.BranchTrue, keyD.Id, AutomationPortIds.FlowIn)
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.D, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_PidOutputIsStableAcrossBranchComparisonsForSameProbeResults()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 10, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 10, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 0, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 10, 12)));
        keyboard.Setup(k => k.TapKey(Key.A, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2
        });
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
        var target = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var current = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var pid = CreateNode("control.pid_controller", new JsonObject
        {
            [AutomationNodePropertyKeys.PidKp] = 0,
            [AutomationNodePropertyKeys.PidKi] = 0,
            [AutomationNodePropertyKeys.PidKd] = 0.001
        });
        var steerRight = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.GreaterThan,
            [AutomationNodePropertyKeys.CompareRight] = 0.15
        });
        var steerLeft = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.LessThan,
            [AutomationNodePropertyKeys.CompareRight] = -0.15
        });
        var keyA = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A"
        });
        var neutral = CreateNode("automation.delay", new JsonObject
        {
            [AutomationNodePropertyKeys.DelayMilliseconds] = 1
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, loop, capture, target, current, pid, steerRight, steerLeft, keyA, neutral],
            Edges =
            [
                Edge(listener.Id, "flow.out", loop.Id, "flow.in"),
                Edge(loop.Id, "loop.body", capture.Id, "flow.in"),
                Edge(capture.Id, "flow.out", target.Id, "flow.in"),
                Edge(capture.Id, "screen.image", target.Id, "haystack.image"),
                Edge(target.Id, "flow.out", current.Id, "flow.in"),
                Edge(capture.Id, "screen.image", current.Id, "haystack.image"),
                Edge(target.Id, "result.x", pid.Id, "target.value"),
                Edge(current.Id, "result.x", pid.Id, "current.value"),
                Edge(current.Id, "flow.out", steerRight.Id, "flow.in"),
                Edge(pid.Id, "control.signal", steerRight.Id, "left"),
                Edge(pid.Id, "control.signal", steerLeft.Id, "left"),
                Edge(steerRight.Id, "branch.false", steerLeft.Id, "flow.in"),
                Edge(steerLeft.Id, "branch.true", keyA.Id, "flow.in"),
                Edge(steerLeft.Id, "branch.false", neutral.Id, "flow.in"),
                Edge(keyA.Id, "flow.out", loop.Id, "flow.in"),
                Edge(neutral.Id, "flow.out", loop.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.A, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_LoopInteriorSkipDocumentStepInterval_Completes()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2,
            [AutomationNodePropertyKeys.LoopInteriorSkipDocumentStepInterval] = true
        });
        var logA = CreateNode("debug.log", new JsonObject
        {
            [AutomationNodePropertyKeys.LogMessage] = "a"
        });
        var logB = CreateNode("debug.log", new JsonObject
        {
            [AutomationNodePropertyKeys.LogMessage] = "b"
        });

        var doc = new AutomationGraphDocument
        {
            MinNodeStepIntervalSeconds = 0.05,
            Nodes = [listener, loop, logA, logB],
            Edges =
            [
                Edge(listener.Id, "flow.out", loop.Id, "flow.in"),
                Edge(loop.Id, "loop.body", logA.Id, "flow.in"),
                Edge(logA.Id, "flow.out", logB.Id, "flow.in"),
                Edge(logB.Id, "flow.out", loop.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
    }

    [Fact]
    public async Task RunOnceAsync_MissingTemplateNeedleRoutesToImageMiss()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        keyboard.Setup(k => k.TapKey(Key.D, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.TemplateMatch,
            [AutomationNodePropertyKeys.FindImageNeedlePath] = @"Z:\missing-template-needle.png"
        });
        var branch = CreateNode("logic.branch_image");
        var keyD = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "D"
        });

        var doc = new AutomationGraphDocument
        {
            VerboseExecutionLogging = true,
            Nodes = [capture, find, branch, keyD],
            Edges =
            [
                Edge(capture.Id, "flow.out", find.Id, "flow.in"),
                Edge(capture.Id, "screen.image", find.Id, "haystack.image"),
                Edge(find.Id, "flow.out", branch.Id, "flow.in"),
                Edge(find.Id, "probe.image", branch.Id, "probe.image"),
                Edge(branch.Id, "branch.miss", keyD.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        Assert.Contains(result.LogLines, line => line.Contains("missing_template_needle", StringComparison.Ordinal));
        keyboard.Verify(k => k.TapKey(Key.D, 1, 0, 70), Times.Once);
        probeService.Verify(
            p => p.ProbeAsync(
                It.IsAny<BitmapSource>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<BitmapSource?>(),
                It.IsAny<AutomationImageProbeOptions>(),
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
            CreateCaptureResolver(captureService.Object),
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
            CreateCaptureResolver(captureService.Object),
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
    public async Task RunOnceAsync_FailsWhenCaptureCacheReferenceIsInvalidGuid()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureCacheRefNodeId] = "not-a-guid"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.False(result.Ok);
        Assert.Equal("AutomationSmoke_RunFailed", result.MessageResourceKey);
        Assert.Equal("capture_screen:cache_ref_invalid", result.Detail);
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
            CreateCaptureResolver(captureService.Object),
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
            CreateCaptureResolver(captureService.Object),
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
    public async Task RunOnceAsync_FailsWhenBranchCompareOperatorIsInvalid()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var branch = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = "approximately"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [branch]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.False(result.Ok);
        Assert.Equal("branch_compare:operator_invalid", result.Detail);
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
        mouse.Setup(m => m.MoveBy(4, -3, 1.0f, null));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
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
        mouse.Verify(m => m.MoveBy(4, -3, 1.0f, null), Times.Once);
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
            CreateCaptureResolver(captureService.Object),
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
            VerboseExecutionLogging = true,
            Nodes = [keyNode]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        Assert.Contains(result.LogLines, line => line.Contains("hold_ms=5", StringComparison.Ordinal));
        keyboard.Verify(k => k.KeyDown(Key.Space), Times.Once);
        keyboard.Verify(k => k.KeyUp(Key.Space), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_HoldWhileTrueMode_PressesAndReleasesKeyboardAcrossLoopIterations()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 9, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 1, 12)));
        keyboard.Setup(k => k.KeyDown(Key.A));
        keyboard.Setup(k => k.KeyUp(Key.A));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2
        });
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var compare = CreateNode("logic.gt", new JsonObject
        {
            [AutomationNodePropertyKeys.CompareRight] = 5
        });
        var key = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A",
            [AutomationNodePropertyKeys.KeyboardActionMode] = AutomationOutputActionModes.HoldWhileTrue
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, loop, capture, find, compare, key],
            Edges =
            [
                Edge(listener.Id, AutomationPortIds.FlowOut, loop.Id, AutomationPortIds.FlowIn),
                Edge(loop.Id, "loop.body", capture.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.FlowOut, find.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.ScreenImage, find.Id, AutomationPortIds.HaystackImage),
                Edge(find.Id, AutomationPortIds.FlowOut, key.Id, AutomationPortIds.FlowIn),
                Edge(find.Id, "result.x", compare.Id, "left"),
                Edge(compare.Id, "value", key.Id, AutomationPortIds.Condition),
                Edge(key.Id, AutomationPortIds.FlowOut, loop.Id, AutomationPortIds.FlowIn)
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.KeyDown(Key.A), Times.Once);
        keyboard.Verify(k => k.KeyUp(Key.A), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_HoldWhileTrueMode_PressesAndReleasesMouseAcrossLoopIterations()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();

        captureService
            .Setup(s => s.CaptureRectanglePhysical(10, 12, 40, 36))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                10,
                12,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                AutomationVisionAlgorithmKind.ColorThreshold,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 9, 12)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 1, 12)));
        mouse.Setup(m => m.LeftDown());
        mouse.Setup(m => m.LeftUp());

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2
        });
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
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var compare = CreateNode("logic.gt", new JsonObject
        {
            [AutomationNodePropertyKeys.CompareRight] = 5
        });
        var click = CreateNode("output.mouse_click", new JsonObject
        {
            [AutomationNodePropertyKeys.MouseActionMode] = AutomationOutputActionModes.HoldWhileTrue,
            [AutomationNodePropertyKeys.MouseButton] = "left"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, loop, capture, find, compare, click],
            Edges =
            [
                Edge(listener.Id, AutomationPortIds.FlowOut, loop.Id, AutomationPortIds.FlowIn),
                Edge(loop.Id, "loop.body", capture.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.FlowOut, find.Id, AutomationPortIds.FlowIn),
                Edge(capture.Id, AutomationPortIds.ScreenImage, find.Id, AutomationPortIds.HaystackImage),
                Edge(find.Id, AutomationPortIds.FlowOut, click.Id, AutomationPortIds.FlowIn),
                Edge(find.Id, "result.x", compare.Id, "left"),
                Edge(compare.Id, "value", click.Id, AutomationPortIds.Condition),
                Edge(click.Id, AutomationPortIds.FlowOut, loop.Id, AutomationPortIds.FlowIn)
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        mouse.Verify(m => m.LeftDown(), Times.Once);
        mouse.Verify(m => m.LeftUp(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_UsesRequestedInputModeForKeyboardNode()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var defaultKeyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var defaultMouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var modeKeyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var modeMouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var modeResolver = new Mock<IAutomationNodeInputModeResolver>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        modeResolver
            .Setup(r => r.Resolve(InputEmulationApiIds.InputInjection))
            .Returns((modeKeyboard.Object, modeMouse.Object));
        modeKeyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            defaultKeyboard.Object,
            defaultMouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy(),
            null,
            null,
            modeResolver.Object);

        var keyNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space",
            [AutomationNodePropertyKeys.InputEmulationApiId] = InputEmulationApiIds.InputInjection
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [keyNode]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        modeResolver.Verify(r => r.Resolve(InputEmulationApiIds.InputInjection), Times.Once);
        modeKeyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_UsesRequestedInputModeForMouseNode()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var defaultKeyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var defaultMouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var modeKeyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var modeMouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var modeResolver = new Mock<IAutomationNodeInputModeResolver>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);

        modeResolver
            .Setup(r => r.Resolve(InputEmulationApiIds.InputInjection))
            .Returns((modeKeyboard.Object, modeMouse.Object));
        modeMouse.Setup(m => m.LeftClick());

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            defaultKeyboard.Object,
            defaultMouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy(),
            null,
            null,
            modeResolver.Object);

        var mouseNode = CreateNode("output.mouse_click", new JsonObject
        {
            [AutomationNodePropertyKeys.InputEmulationApiId] = InputEmulationApiIds.InputInjection
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [mouseNode]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        modeResolver.Verify(r => r.Resolve(InputEmulationApiIds.InputInjection), Times.Once);
        modeMouse.Verify(m => m.LeftClick(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_InProcessCaptureRoutesKeyboardOutputToKeyboardEmulator()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var processTargetService = new Mock<IProcessTargetService>(MockBehavior.Loose);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy(),
            outputGuard: new AutomationRuntimeOutputGuard(processTargetService.Object));

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        });
        var keyboardNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, keyboardNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", keyboardNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_InProcessRoiCaptureRoutesKeyboardOutputToKeyboardEmulator()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap(4, 4);
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(10, 20, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureRectanglePhysical(11, 22, 2, 2))
            .Returns(CreateBitmap(2, 2));
        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame",
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 11,
                ["y"] = 22,
                ["width"] = 2,
                ["height"] = 2
            }
        });
        var keyboardNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, keyboardNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", keyboardNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
        captureService.Verify(s => s.CaptureRectanglePhysical(11, 22, 2, 2), Times.Once);
        captureService.Verify(s => s.CaptureProcessWindowPhysical(It.IsAny<AutomationProcessWindowTarget>()), Times.Never);
    }

    [Fact]
    public async Task RunOnceAsync_InProcessCaptureWithResolvedTargetRoutesKeyboardOutputToKeyboardEmulator()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var resolvedTarget = AutomationProcessWindowTarget.From("MyGame", 4242);
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
            resolvedTarget);

        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        });
        var keyboardNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, keyboardNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", keyboardNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_InProcessCaptureRoutesMouseOutputToMouseEmulator()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var virtualMouse = new Mock<IVirtualScreenMouse>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        virtualMouse.Setup(v => v.MoveCursorToVirtualScreenPixels(123, 456));
        mouse.Setup(m => m.LeftClick());

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
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
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        });
        var mouseNode = CreateNode("output.mouse_click", new JsonObject
        {
            [AutomationNodePropertyKeys.MouseCoordinateMode] = "absolute",
            [AutomationNodePropertyKeys.MouseAbsoluteX] = 123,
            [AutomationNodePropertyKeys.MouseAbsoluteY] = 456
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, mouseNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", mouseNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        virtualMouse.Verify(v => v.MoveCursorToVirtualScreenPixels(123, 456), Times.Once);
        mouse.Verify(m => m.LeftClick(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_InProcessMouseWithoutTargetFallsBackToGlobalClick()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        mouse.Setup(m => m.LeftClick());

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        });
        var mouseNode = CreateNode("output.mouse_click");
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, mouseNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", mouseNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        mouse.Verify(m => m.LeftClick(), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_CachedInProcessCaptureUsesKeyboardEmulator()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureProcessWindowPhysical(
                It.Is<AutomationProcessWindowTarget>(target =>
                    target.ProcessName == "MyGame" && target.ProcessId == 0)))
            .Returns(captureResult);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var sourceCapture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.InProcessWindow,
            [AutomationNodePropertyKeys.CaptureProcessName] = "MyGame"
        });
        var cachedCapture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureCacheRefNodeId] = sourceCapture.Id.ToString()
        });
        var keyboardNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [sourceCapture, cachedCapture, keyboardNode],
            Edges =
            [
                Edge(sourceCapture.Id, "flow.out", cachedCapture.Id, "flow.in"),
                Edge(cachedCapture.Id, "flow.out", keyboardNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
        captureService.Verify(s => s.CaptureProcessWindowPhysical(
            It.Is<AutomationProcessWindowTarget>(target =>
                target.ProcessName == "MyGame" && target.ProcessId == 0)), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_GlobalCaptureDoesNotRequireForegroundProcessForOutput()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var processTargetService = new Mock<IProcessTargetService>(MockBehavior.Strict);
        var processDispatcher = new Mock<IAutomationProcessWindowInputDispatcher>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        var captureResult = new AutomationVirtualScreenCaptureResult(
            bitmap,
            new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

        captureService.Setup(s => s.CaptureVirtualScreenPhysical()).Returns(captureResult);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy(),
            outputGuard: new AutomationRuntimeOutputGuard(processTargetService.Object),
            processWindowInputDispatcher: processDispatcher.Object);

        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Full,
            [AutomationNodePropertyKeys.CaptureSourceMode] = AutomationCaptureSourceMode.Screen
        });
        var keyboardNode = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });
        var doc = new AutomationGraphDocument
        {
            Nodes = [capture, keyboardNode],
            Edges =
            [
                Edge(capture.Id, "flow.out", keyboardNode.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
        processDispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_ExecutesMacroSubgraphAndReturnsToParentFlow()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));
        keyboard.Setup(k => k.TapKey(Key.A, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var macroNode = CreateNode("automation.macro", new JsonObject
        {
            [AutomationNodePropertyKeys.MacroSubgraphId] = "combat.opening"
        });
        var followUp = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A"
        });
        var subgraphKey = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [macroNode, followUp],
            Edges =
            [
                Edge(macroNode.Id, "flow.out", followUp.Id, "flow.in")
            ],
            Subgraphs =
            [
                new AutomationSubgraphDefinition
                {
                    Id = "combat.opening",
                    DisplayName = "Combat Opening",
                    Graph = new AutomationGraphDocument
                    {
                        Nodes = [subgraphKey]
                    }
                }
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
        keyboard.Verify(k => k.TapKey(Key.A, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_TriggersEventListenerAfterEventEmit()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var emitter = CreateNode("event.emit", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "combat.start"
        });
        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "combat.start"
        });
        var action = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [emitter, listener, action],
            Edges =
            [
                Edge(listener.Id, "flow.out", action.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(result.Ok);
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_PidDeadbandSuppressesSmallError()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        keyboard.Setup(k => k.TapKey(Key.Space, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var pid = CreateNode("control.pid_controller", new JsonObject
        {
            [AutomationNodePropertyKeys.PidCurrentValue] = 9.8,
            [AutomationNodePropertyKeys.PidTargetValue] = 10,
            [AutomationNodePropertyKeys.PidKp] = 10,
            [AutomationNodePropertyKeys.PidDeadband] = 0.5
        });
        var compare = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.GreaterThan,
            [AutomationNodePropertyKeys.CompareRight] = 0
        });
        var action = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "Space"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, pid, compare, action],
            Edges =
            [
                Edge(listener.Id, "flow.out", compare.Id, "flow.in"),
                Edge(pid.Id, "control.signal", compare.Id, "left"),
                Edge(compare.Id, AutomationPortIds.BranchFalse, action.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.Space, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_PidOutputClampLimitsControlSignal()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        keyboard.Setup(k => k.TapKey(Key.A, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var pid = CreateNode("control.pid_controller", new JsonObject
        {
            [AutomationNodePropertyKeys.PidCurrentValue] = 0,
            [AutomationNodePropertyKeys.PidTargetValue] = 10,
            [AutomationNodePropertyKeys.PidKp] = 10,
            [AutomationNodePropertyKeys.PidOutputMin] = -3,
            [AutomationNodePropertyKeys.PidOutputMax] = 3
        });
        var compare = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.EqualTo,
            [AutomationNodePropertyKeys.CompareRight] = 3
        });
        var action = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, pid, compare, action],
            Edges =
            [
                Edge(listener.Id, "flow.out", compare.Id, "flow.in"),
                Edge(pid.Id, "control.signal", compare.Id, "left"),
                Edge(compare.Id, AutomationPortIds.BranchTrue, action.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.A, 1, 0, 70), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_SignalConditioningNodesFilterVisionCoordinates()
    {
        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Strict);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Strict);
        var registry = new NodeTypeRegistry();
        var topology = new AutomationTopologyAnalyzer(registry);
        var bitmap = CreateBitmap();
        captureService
            .Setup(s => s.CaptureRectanglePhysical(0, 0, bitmap.PixelWidth, bitmap.PixelHeight))
            .Returns(bitmap);
        probeService
            .SetupSequence(p => p.ProbeAsync(
                bitmap,
                0,
                0,
                null,
                It.IsAny<AutomationImageProbeOptions>(),
                It.IsAny<AutomationVisionAlgorithmKind>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 0, 0)))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(true, 100, 0)));
        keyboard.Setup(k => k.TapKey(Key.A, 1, 0, 70));

        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            topology,
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var listener = CreateNode("event.listener", new JsonObject
        {
            [AutomationNodePropertyKeys.EventSignal] = "engine.start"
        });
        var loop = CreateNode("automation.loop", new JsonObject
        {
            [AutomationNodePropertyKeys.LoopMaxIterations] = 2
        });
        var capture = CreateNode("perception.capture_screen", new JsonObject
        {
            [AutomationNodePropertyKeys.CaptureMode] = AutomationCaptureMode.Roi,
            [AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["width"] = bitmap.PixelWidth,
                ["height"] = bitmap.PixelHeight
            }
        });
        var find = CreateNode("perception.find_image", new JsonObject
        {
            [AutomationNodePropertyKeys.FindImageAlgorithm] = AutomationVisionAlgorithmStorage.ColorThreshold
        });
        var smooth = CreateNode(AutomationNodeTypeIds.SignalSmooth, new JsonObject
        {
            [AutomationNodePropertyKeys.SmoothFactor] = 0.5
        });
        var clamp = CreateNode(AutomationNodeTypeIds.MathClamp, new JsonObject
        {
            [AutomationNodePropertyKeys.MathMin] = 0,
            [AutomationNodePropertyKeys.MathMax] = 40
        });
        var deadband = CreateNode(AutomationNodeTypeIds.MathDeadband, new JsonObject
        {
            [AutomationNodePropertyKeys.MathThreshold] = 30
        });
        var compare = CreateNode(AutomationNodeTypeIds.BranchCompare, new JsonObject
        {
            [AutomationNodePropertyKeys.CompareOperator] = AutomationComparisonEvaluator.GreaterThan,
            [AutomationNodePropertyKeys.CompareRight] = 0
        });
        var action = CreateNode("output.keyboard_key", new JsonObject
        {
            [AutomationNodePropertyKeys.KeyboardKey] = "A"
        });

        var doc = new AutomationGraphDocument
        {
            Nodes = [listener, loop, capture, find, smooth, clamp, deadband, compare, action],
            Edges =
            [
                Edge(listener.Id, "flow.out", loop.Id, "flow.in"),
                Edge(loop.Id, "loop.body", capture.Id, "flow.in"),
                Edge(capture.Id, "flow.out", find.Id, "flow.in"),
                Edge(capture.Id, "screen.image", find.Id, "haystack.image"),
                Edge(find.Id, "flow.out", compare.Id, "flow.in"),
                Edge(find.Id, "result.x", smooth.Id, "input"),
                Edge(smooth.Id, "value", clamp.Id, "input"),
                Edge(clamp.Id, "value", deadband.Id, "input"),
                Edge(deadband.Id, "value", compare.Id, "left"),
                Edge(compare.Id, AutomationPortIds.BranchTrue, action.Id, "flow.in"),
                Edge(compare.Id, AutomationPortIds.BranchFalse, loop.Id, "flow.in"),
                Edge(action.Id, "flow.out", loop.Id, "flow.in")
            ]
        };

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        keyboard.Verify(k => k.TapKey(Key.A, 1, 0, 70), Times.Once);
    }

    [Fact]
    public void AutomationNeedlePathResolver_FindsRelativePathUnderContentRoot()
    {
        var expectedPath = Path.GetFullPath(Path.Combine(
            AppPaths.ResolveContentRoot(),
            "Assets",
            "Automation",
            "Samples",
            "fishing-mini-game-bot",
            "needle-fishing-ui.png"));
        if (!File.Exists(expectedPath))
            return;

        var resolved = AutomationNeedlePathResolver.ResolveExistingFilePath(
            "Assets/Automation/Samples/fishing-mini-game-bot/needle-fishing-ui.png");

        Assert.Equal(expectedPath, resolved);
    }

    [Fact]
    public async Task RunOnceAsync_FishingSample_CompletesOnMissPath()
    {
        var path = Path.Combine(AppPaths.ResolveContentRoot(), "Assets", "Automation", "Samples", "fishing-mini-game-bot.json");
        if (!File.Exists(path))
            return;

        var json = await File.ReadAllTextAsync(path);
        var serializer = new AutomationGraphJsonSerializer();
        var doc = serializer.Deserialize(json);
        const int bitmapWidth = 2;
        const int bitmapHeight = 2;
        foreach (var node in doc.Nodes)
        {
            node.Properties ??= new JsonObject();
            if (string.Equals(node.NodeTypeId, "automation.loop", StringComparison.Ordinal))
                AutomationNodePropertyReader.WriteInt(node.Properties, AutomationNodePropertyKeys.LoopMaxIterations, 2);
            if (string.Equals(node.NodeTypeId, "automation.delay", StringComparison.Ordinal))
                AutomationNodePropertyReader.WriteInt(node.Properties, AutomationNodePropertyKeys.DelayMilliseconds, 0);
            if (string.Equals(node.NodeTypeId, "perception.capture_screen", StringComparison.Ordinal) &&
                AutomationCaptureSourceMode.IsInProcessWindow(
                    AutomationNodePropertyReader.ReadString(node.Properties, AutomationNodePropertyKeys.CaptureSourceMode)))
            {
                node.Properties[AutomationNodePropertyKeys.CaptureRoi] = new JsonObject
                {
                    ["x"] = 0,
                    ["y"] = 0,
                    ["width"] = bitmapWidth,
                    ["height"] = bitmapHeight
                };
            }
        }

        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Loose);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Loose);
        var bitmap = CreateBitmap(bitmapWidth, bitmapHeight);
        var metrics = new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
        captureService.Setup(s => s.CaptureVirtualScreenPhysical())
            .Returns(new AutomationVirtualScreenCaptureResult(bitmap, metrics));
        captureService.Setup(s => s.CaptureProcessWindowPhysical(It.IsAny<AutomationProcessWindowTarget>()))
            .Returns(new AutomationVirtualScreenCaptureResult(bitmap, metrics));
        captureService.Setup(s =>
                s.CaptureRectanglePhysical(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(bitmap);
        probeService.Setup(p => p.ProbeAsync(
            It.IsAny<BitmapSource>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<BitmapSource?>(),
            It.IsAny<AutomationImageProbeOptions>(),
            It.IsAny<AutomationVisionAlgorithmKind>(),
            It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(false, 0, 0, 0, 0, 0)));

        var registry = new NodeTypeRegistry();
        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            new AutomationTopologyAnalyzer(registry),
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
    }

    [Fact]
    public async Task RunOnceAsync_BenchmarkSample_Completes()
    {
        var path = Path.Combine(AppPaths.ResolveContentRoot(), "Assets", "Automation", "Samples", "benchmark-test.json");
        if (!File.Exists(path))
            return;

        var json = await File.ReadAllTextAsync(path);
        var serializer = new AutomationGraphJsonSerializer();
        var doc = serializer.Deserialize(json);
        const int bitmapWidth = 640;
        const int bitmapHeight = 360;
        foreach (var node in doc.Nodes)
        {
            node.Properties ??= new JsonObject();
            if (string.Equals(node.NodeTypeId, "automation.loop", StringComparison.Ordinal))
                AutomationNodePropertyReader.WriteInt(node.Properties, AutomationNodePropertyKeys.LoopMaxIterations, 30);
        }

        var captureService = new Mock<IAutomationScreenCaptureService>(MockBehavior.Strict);
        var probeService = new Mock<IAutomationImageProbe>(MockBehavior.Strict);
        var keyboard = new Mock<IKeyboardEmulator>(MockBehavior.Loose);
        var mouse = new Mock<IMouseEmulator>(MockBehavior.Loose);
        var bitmap = CreateBitmap(bitmapWidth, bitmapHeight);
        var metrics = new AutomationVirtualScreenMetrics(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
        captureService.Setup(s => s.CaptureVirtualScreenPhysical())
            .Returns(new AutomationVirtualScreenCaptureResult(bitmap, metrics));
        captureService.Setup(s =>
                s.CaptureRectanglePhysical(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(bitmap);
        probeService.Setup(p => p.ProbeAsync(
            It.IsAny<BitmapSource>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<BitmapSource?>(),
            It.IsAny<AutomationImageProbeOptions>(),
            It.IsAny<AutomationVisionAlgorithmKind>(),
            It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(new AutomationImageProbeResult(false, 0, 0, 0, 0, 0)));

        var registry = new NodeTypeRegistry();
        var sut = new AutomationGraphSmokeRunner(
            CreateCaptureResolver(captureService.Object),
            probeService.Object,
            keyboard.Object,
            mouse.Object,
            null,
            registry,
            new AutomationTopologyAnalyzer(registry),
            new AutomationNodeContractValidator(),
            new AutomationExecutionSafetyPolicy());

        var result = await sut.RunOnceAsync(doc);

        Assert.True(
            result.Ok,
            $"{result.MessageResourceKey}::{result.Detail}::{string.Join(" | ", result.LogLines)}");
        probeService.Verify(p => p.ProbeAsync(
            It.IsAny<BitmapSource>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<BitmapSource?>(),
            It.IsAny<AutomationImageProbeOptions>(),
            It.IsAny<AutomationVisionAlgorithmKind>(),
            It.IsAny<CancellationToken>()), Times.Exactly(30));
    }

    private sealed class FixedTapHoldNoiseController(int adjustedHoldMs) : IHumanInputNoiseController
    {
        public int AdjustDelayMs(int baseDelayMs) => baseDelayMs;

        public int AdjustTapHoldMs(int nominalMs, int maxDeviationMs) => adjustedHoldMs;

        public (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY, float stickMagnitude = 1.0f) =>
            (deltaX, deltaY);
    }

    private static IAutomationScreenCaptureServiceResolver CreateCaptureResolver(
        IAutomationScreenCaptureService capture) =>
        new AutomationScreenCaptureServiceResolver(
            new Dictionary<string, IAutomationScreenCaptureService>(StringComparer.OrdinalIgnoreCase)
            {
                [AutomationCaptureApi.Gdi] = capture,
                [AutomationCaptureApi.DesktopDuplication] = capture
            },
            AutomationCaptureApi.Gdi);

    private static BitmapSource CreateBitmap(int width = 2, int height = 2)
    {
        var stride = width * 4;
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[stride * height], stride);
        if (bitmap.CanFreeze)
            bitmap.Freeze();

        return bitmap;
    }

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
