using Gamepad_Mapping.Interfaces.Services;
using Gamepad_Mapping.Services;
using Gamepad_Mapping.ViewModels;
using GamepadMapperGUI.Interfaces.Services;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GamepadMapperGUI.Services;

namespace Gamepad_Mapping;

public partial class App : Application
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    public static ILogger Logger { get; private set; } = new FileLogger();

    /// <summary>Matches the last-applied Windows app theme (updated in <see cref="ApplySystemTheme"/>).</summary>
    public static bool UsesLightTheme { get; private set; } = true;

    /// <summary>Fired after application resource brushes are refreshed for light/dark mode.</summary>
    public static event EventHandler? ThemeChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Info("Application starting...");
        ApplyLanguage();
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        CheckStartupElevationCompatibility();

        var gitHubContentService = new GitHubContentService();
        var profileService = new ProfileService();
        var localFileService = new LocalFileService();
        var updateInstallerService = new UpdateInstallerService();
        var settingsService = new SettingsService();
        var appSettings = settingsService.LoadSettingsInternal();
        var updateVersionCacheService = new UpdateVersionCacheService();
        var trustedUtcTimeService = new TrustedUtcTimeService();
        var updateQuotaPolicyProvider = new StaticUpdateQuotaPolicyProvider();
        var updateQuotaService = new UpdateQuotaService(updateQuotaPolicyProvider, trustedUtcTimeService);
        var mainViewModel = new MainViewModel(
            profileService: profileService,
            gitHubContentService: gitHubContentService,
            communityService: new CommunityTemplateService(profileService, gitHubContentService, localFileService),
            updateService: new UpdateService(gitHubContentService, settingsService, appSettings, updateVersionCacheService),
            localFileService: localFileService,
            updateInstallerService: updateInstallerService,
            updateQuotaService: updateQuotaService,
            settingsService: settingsService,
            trustedUtcTimeService: trustedUtcTimeService,
            updateVersionCacheService: updateVersionCacheService,
            updateQuotaPolicyProvider: updateQuotaPolicyProvider);

        var mainWindow = new MainWindow(mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"Application exiting with code {e.ApplicationExitCode}");
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            ApplySystemTheme();
    }

    private void ApplySystemTheme()
    {
        var useLightTheme = ReadUseLightTheme();
        UsesLightTheme = useLightTheme;

        if (useLightTheme)
        {
            SetBrush("AppBackgroundBrush", Color.FromRgb(247, 247, 247));
            SetBrush("AppSurfaceBrush", Colors.White);
            SetBrush("AppBorderBrush", Color.FromRgb(51, 51, 51));
            SetBrush("AppTextBrush", Color.FromRgb(17, 17, 17));
            SetBrush("AppSecondaryTextBrush", Color.FromRgb(85, 85, 85));
            SetBrush("AppAccentTextBrush", Color.FromRgb(204, 51, 0));
            SetBrush("AppControlSurfaceBrush", Color.FromRgb(255, 255, 255));
            SetBrush("AppControlSurfaceAltBrush", Color.FromRgb(241, 241, 241));
            SetBrush("AppControlHoverBrush", Color.FromRgb(232, 232, 232));
            SetBrush("AppControlPressedBrush", Color.FromRgb(220, 220, 220));
            SetBrush("AppAccentBrush", Color.FromRgb(28, 65, 138));
            SetBrush("AppAccentHoverBrush", Color.FromRgb(22, 52, 115));
            SetBrush("AppSelectionBrush", Color.FromRgb(225, 235, 255));
            SetBrush("AppSelectionTextBrush", Color.FromRgb(17, 17, 17));
            SetBrush("AppScrollTrackBrush", Color.FromRgb(229, 229, 229));
            SetBrush("AppScrollThumbBrush", Color.FromRgb(184, 184, 184));
            SetBrush("AppScrollThumbHoverBrush", Color.FromRgb(154, 154, 154));
            SetBrush("AppSeparatorBrush", Color.FromRgb(218, 218, 224));
            SetBrush("AppGridSplitterBrush", Color.FromRgb(200, 200, 210));
            SetBrush("AppGridSplitterHoverBrush", Color.FromRgb(28, 65, 138));
            SetBrush("AppHudTitleBrush", Color.FromRgb(26, 26, 30));
            SetBrush("AppHudDetailBrush", Color.FromRgb(75, 78, 90));
        }
        else
        {
            SetBrush("AppBackgroundBrush", Color.FromRgb(24, 24, 27));
            SetBrush("AppSurfaceBrush", Color.FromRgb(35, 35, 40));
            SetBrush("AppBorderBrush", Color.FromRgb(95, 95, 105));
            SetBrush("AppTextBrush", Color.FromRgb(238, 238, 242));
            SetBrush("AppSecondaryTextBrush", Color.FromRgb(185, 185, 195));
            SetBrush("AppAccentTextBrush", Color.FromRgb(255, 135, 95));
            SetBrush("AppControlSurfaceBrush", Color.FromRgb(43, 43, 49));
            SetBrush("AppControlSurfaceAltBrush", Color.FromRgb(50, 50, 58));
            SetBrush("AppControlHoverBrush", Color.FromRgb(62, 62, 72));
            SetBrush("AppControlPressedBrush", Color.FromRgb(78, 78, 91));
            SetBrush("AppAccentBrush", Color.FromRgb(50, 100, 198));
            SetBrush("AppAccentHoverBrush", Color.FromRgb(72, 120, 225));
            SetBrush("AppSelectionBrush", Color.FromRgb(62, 83, 122));
            SetBrush("AppSelectionTextBrush", Color.FromRgb(245, 247, 250));
            SetBrush("AppScrollTrackBrush", Color.FromRgb(54, 54, 63));
            SetBrush("AppScrollThumbBrush", Color.FromRgb(104, 104, 116));
            SetBrush("AppScrollThumbHoverBrush", Color.FromRgb(132, 132, 146));
            SetBrush("AppSeparatorBrush", Color.FromRgb(62, 62, 72));
            SetBrush("AppGridSplitterBrush", Color.FromRgb(72, 72, 84));
            SetBrush("AppGridSplitterHoverBrush", Color.FromRgb(50, 100, 198));
            SetBrush("AppHudTitleBrush", Color.FromRgb(245, 246, 250));
            SetBrush("AppHudDetailBrush", Color.FromRgb(205, 210, 220));
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

    private void SetBrush(string resourceKey, Color color)
    {
        // Some XAML-created brushes can be frozen (read-only), so always replace the resource instance.
        Resources[resourceKey] = new SolidColorBrush(color);
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

