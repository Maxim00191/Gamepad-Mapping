using System;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Infrastructure;

namespace Gamepad_Mapping;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Register global handlers as early as possible
        StartupDiagnostics.RegisterHandlers(App.Logger);

        if (TryRunAutomationScriptMode(args))
            return;

        var app = new App();

        try
        {
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            App.Logger.Error("Fatal exception during application lifecycle", ex);
            StartupDiagnostics.ShowFatalErrorDialog(ex);
        }
    }

    private static bool TryRunAutomationScriptMode(string[] args)
    {
        if (!TryGetOptionValue(args, "--automation-script", out var scriptPath))
            return false;

        try
        {
            var settingsService = new SettingsService();
            var settings = settingsService.LoadSettingsInternal();
            var stackFactory = new InputEmulationStackFactory(
                () => GamepadInputStreamConstraints.ClampPollingIntervalMs(settings.GamepadPollingIntervalMs));
            var (keyboard, mouse) = stackFactory.CreatePair(
                settings.InputEmulationApi,
                () => HumanInputNoiseParameters.From(settings));
            var inputModeResolver = new AutomationNodeInputModeResolver(keyboard, mouse);
            var humanNoise = new HumanInputNoiseController(
                new NoiseGenerator(Random.Shared.Next()),
                () => HumanInputNoiseParameters.From(settings),
                new RealTimeProvider());

            var executionFactory = new AutomationExecutionServicesFactory();
            var execution = executionFactory.Create(keyboard, mouse, humanNoise, inputModeResolver);
            var serializer = new AutomationGraphJsonSerializer();
            var scriptRunner = new AutomationScriptRunner(serializer, execution.SmokeRunner);

            if (!TryGetOptionValue(args, "--automation-interval-ms", out var intervalText))
            {
                RunAutomationScriptOnce(scriptRunner, scriptPath!);
                return true;
            }

            var intervalMs = ParsePositiveInterval(intervalText);
            RunAutomationScriptLoop(scriptRunner, scriptPath!, intervalMs);
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Automation script mode failed", ex);
            return true;
        }
    }

    private static void RunAutomationScriptOnce(AutomationScriptRunner scriptRunner, string scriptPath)
    {
        var result = scriptRunner.RunFileOnceAsync(scriptPath).GetAwaiter().GetResult();
        LogScriptRunResult(result);
    }

    private static void RunAutomationScriptLoop(AutomationScriptRunner scriptRunner, string scriptPath, int intervalMs)
    {
        while (true)
        {
            var result = scriptRunner.RunFileOnceAsync(scriptPath).GetAwaiter().GetResult();
            LogScriptRunResult(result);
            Thread.Sleep(intervalMs);
        }
    }

    private static void LogScriptRunResult(AutomationSmokeRunResult result)
    {
        var level = result.Ok ? "info" : "warn";
        App.Logger.Info($"[automation-script:{level}] ok={result.Ok} key={result.MessageResourceKey} detail={result.Detail}");
        foreach (var line in result.LogLines)
            App.Logger.Info($"[automation-script] {line}");
    }

    private static int ParsePositiveInterval(string? raw)
    {
        if (int.TryParse(raw, out var parsed) && parsed > 0)
            return parsed;
        return 1000;
    }

    private static bool TryGetOptionValue(string[] args, string optionName, out string? value)
    {
        value = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length)
                return false;
            value = args[i + 1];
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}
