using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace Gamepad_Mapping;

public partial class App : Application
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
            SetBrush("AppAccentBrush", Color.FromRgb(46, 107, 216));
            SetBrush("AppAccentHoverBrush", Color.FromRgb(37, 88, 177));
            SetBrush("AppSelectionBrush", Color.FromRgb(225, 235, 255));
            SetBrush("AppSelectionTextBrush", Color.FromRgb(17, 17, 17));
            return;
        }

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
        SetBrush("AppAccentBrush", Color.FromRgb(79, 140, 255));
        SetBrush("AppAccentHoverBrush", Color.FromRgb(104, 157, 255));
        SetBrush("AppSelectionBrush", Color.FromRgb(62, 83, 122));
        SetBrush("AppSelectionTextBrush", Color.FromRgb(245, 247, 250));
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
}

