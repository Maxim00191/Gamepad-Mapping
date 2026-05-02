using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Utils;
using Gamepad_Mapping.Utils.Theme;
using System.IO;
using System.Linq;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Core;
using System.Windows.Interop;

namespace Gamepad_Mapping;

public partial class App : Application
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    public static ILogger Logger { get; private set; } = new FileLogger();

    /// <summary>Matches the last-applied effective theme (updated in <see cref="ApplyChromeTheme"/>).</summary>
    public static bool UsesLightTheme { get; private set; } = true;

    /// <summary>Fired after application resource brushes are refreshed for light/dark mode.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>Singleton corner toast API; safe to call from background threads (marshals to the UI thread).</summary>
    public static IAppToastService ToastService { get; private set; } = null!;
    public static UpdateSuccessArgs? LaunchUpdateSuccessArgs { get; set; }
    private IWindowTitleBarThemeService _windowTitleBarThemeService = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Logger.Info("Application starting...");
#if DEBUG
            try
            {
                var debugPath = Path.Combine(AppPaths.ResolveContentRoot(), "__DEBUG_APPPATHS_ROOT.txt");
                File.WriteAllText(debugPath, $"Content root: {AppPaths.ResolveContentRoot()}\nBase Directory: {AppContext.BaseDirectory}\nProcess Path: {Environment.ProcessPath}\nCurrent Directory: {Directory.GetCurrentDirectory()}");
                Logger.Info($"Wrote debug app paths to {debugPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write debug app paths: {ex.Message}");
            }
#endif
            LaunchUpdateSuccessArgs = ParseUpdateSuccessArgs(e.Args);
            ApplyLanguage();
            _windowTitleBarThemeService = new WindowTitleBarThemeService();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
            ApplyChromeTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            if (ShouldAbortStartupForElevationRelaunch())
                return;

            var (mainViewModel, appToastService) = ApplicationComposition.BuildMainViewModel();
            ToastService = appToastService;

            var mainWindow = new MainWindow(mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Logger.Error("Critical failure during application startup", ex);
            StartupDiagnostics.ShowFatalErrorDialog(ex);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"Application exiting with code {e.ApplicationExitCode}");
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }

    private static UpdateSuccessArgs? ParseUpdateSuccessArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--updated", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return new UpdateSuccessArgs(args[i + 1].Trim());
            }
            if (args[i].StartsWith("--updated ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = args[i].Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) return new UpdateSuccessArgs(parts[1].Trim());
            }
        }
        return null;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
            return;

        if (Dispatcher.CheckAccess())
            ApplyChromeThemeIfFollowingSystem();
        else
            Dispatcher.BeginInvoke(ApplyChromeThemeIfFollowingSystem, DispatcherPriority.Normal);
    }

    public static void RequestApplyChromeTheme()
    {
        if (Current is App app)
            app.ApplyChromeTheme();
    }

    private void ApplyChromeThemeIfFollowingSystem()
    {
        var settings = SettingsService.LoadSettings();
        if (!UiThemeMode.IsFollowSystem(settings.UiTheme))
            return;
        ApplyChromeTheme();
    }

    private void ApplyChromeTheme()
    {
        var settings = SettingsService.LoadSettings();
        var useLightTheme = UiThemeMode.ResolveToLight(settings.UiTheme, ReadUseLightTheme);
        UsesLightTheme = useLightTheme;
        var chromeTheme = useLightTheme ? AppChromeTheme.Light : AppChromeTheme.Dark;

        if (useLightTheme)
        {
            chromeTheme.ApplyTo(Resources);
            VisualWorkspaceTheme.Apply(Resources, light: true);
            AutomationWorkspaceTheme.Apply(Resources, light: true);
        }
        else
        {
            chromeTheme.ApplyTo(Resources);
            VisualWorkspaceTheme.Apply(Resources, light: false);
            AutomationWorkspaceTheme.Apply(Resources, light: false);
        }

        ApplyTitleBarThemeToOpenWindows(chromeTheme, useLightTheme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Window window)
            return;

        var theme = UsesLightTheme ? AppChromeTheme.Light : AppChromeTheme.Dark;
        ApplyTitleBarTheme(window, theme, UsesLightTheme);
    }

    private void ApplyTitleBarThemeToOpenWindows(AppChromeTheme theme, bool usesLightTheme)
    {
        foreach (var window in Current.Windows.OfType<Window>())
            ApplyTitleBarTheme(window, theme, usesLightTheme);
    }

    private void ApplyTitleBarTheme(Window window, AppChromeTheme theme, bool usesLightTheme)
    {
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            _windowTitleBarThemeService.TryApply(
                window,
                theme.TitleBarBackground,
                theme.TitleBarForeground,
                theme.TitleBarBorder,
                usesLightTheme);
            return;
        }

        void ApplyWhenReady(object? _, EventArgs __)
        {
            window.SourceInitialized -= ApplyWhenReady;
            _windowTitleBarThemeService.TryApply(
                window,
                theme.TitleBarBackground,
                theme.TitleBarForeground,
                theme.TitleBarBorder,
                usesLightTheme);
        }

        window.SourceInitialized -= ApplyWhenReady;
        window.SourceInitialized += ApplyWhenReady;
    }

    private static bool ReadUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
            var rawValue = key?.GetValue(AppsUseLightThemeValueName);
            if (rawValue is int intValue)
                return intValue != 0;
        }
        catch
        {
            // Fall back to light theme if registry access is unavailable.
        }

        return true;
    }

    private void ApplyLanguage()
    {
        var settings = SettingsService.LoadSettings();
        var cultureName = string.IsNullOrWhiteSpace(settings.UiCulture) ? "zh-CN" : settings.UiCulture;
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo("zh-CN");
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (Resources["Loc"] is TranslationService translationService)
            translationService.Culture = culture;
    }

    /// <summary>
    /// If the foreground window is an elevated process while we are not, offer to relaunch elevated.
    /// When relaunch succeeds, returns true so <see cref="OnStartup"/> does not construct the main window (shutdown is asynchronous).
    /// </summary>
    private static bool ShouldAbortStartupForElevationRelaunch()
    {
        var processTargetService = new ProcessTargetService();
        if (processTargetService.IsCurrentProcessElevated())
            return false;

        var foregroundPid = processTargetService.GetForegroundProcessId();
        if (foregroundPid <= 0 || !processTargetService.IsProcessElevated(foregroundPid))
            return false;

        var result = new UserDialogService().ConfirmYesNo(
            AppUiLocalization.GetString("ElevationRelaunch_Message"),
            AppUiLocalization.GetString("ElevationRelaunch_Title"),
            MessageBoxImage.Information);
        if (!result)
            return false;

        return ElevationApplicationRelaunch.TryRelaunchElevatedAndShutdownCurrentApplication();
    }
}


