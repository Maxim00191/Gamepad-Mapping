using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
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
            ApplyChromeTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            CheckStartupElevationCompatibility();

            var gitHubContentService = new GitHubContentService();
            var localFileService = new LocalFileService();
            var updateInstallerService = new UpdateInstallerService();
            var settingsService = new SettingsService();
            var appSettings = settingsService.LoadSettingsInternal();
            var profileService = new ProfileService(settingsService: settingsService, appSettings: appSettings);
            var updateVersionCacheService = new UpdateVersionCacheService();
            var trustedUtcTimeService = new TrustedUtcTimeService();
            var updateQuotaPolicyProvider = new StaticUpdateQuotaPolicyProvider();
            var updateQuotaService = new UpdateQuotaService(updateQuotaPolicyProvider, trustedUtcTimeService);
            var appToastService = new AppToastService();
            ToastService = appToastService;
            var xinputService = new XInputService();
            var gamepadSource = new XInputSource(xinputService);
            var mainViewModel = new MainViewModel(
                profileService: profileService,
                gitHubContentService: gitHubContentService,
                communityService: new CommunityTemplateService(profileService, gitHubContentService, localFileService, appSettings),
                updateService: new UpdateService(gitHubContentService, settingsService, appSettings, updateVersionCacheService),
                localFileService: localFileService,
                updateInstallerService: updateInstallerService,
                updateQuotaService: updateQuotaService,
                settingsService: settingsService,
                trustedUtcTimeService: trustedUtcTimeService,
                updateVersionCacheService: updateVersionCacheService,
                updateQuotaPolicyProvider: updateQuotaPolicyProvider,
                appToastService: appToastService,
                xinput: xinputService,
                gamepadSource: gamepadSource);

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

        if (useLightTheme)
        {
            AppChromeTheme.Light.ApplyTo(Resources);
            VisualWorkspaceTheme.Apply(Resources, light: true);
        }
        else
        {
            AppChromeTheme.Dark.ApplyTo(Resources);
            VisualWorkspaceTheme.Apply(Resources, light: false);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
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

    private static void CheckStartupElevationCompatibility()
    {
        var processTargetService = new ProcessTargetService();
        if (processTargetService.IsCurrentProcessElevated())
            return;

        var foregroundPid = processTargetService.GetForegroundProcessId();
        if (foregroundPid <= 0 || !processTargetService.IsProcessElevated(foregroundPid))
            return;

        var result = MessageBox.Show(
            "The currently focused target appears to be running as administrator.\n\n" +
            "To avoid Windows UIPI input blocking, relaunch this tool as administrator?",
            "Run as administrator",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes)
            return;

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });
            Current?.Shutdown();
        }
        catch (Win32Exception)
        {
            // User cancelled the UAC prompt.
        }
        catch
        {
            // Best-effort relaunch only.
        }
    }
}


