using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.Utils;
using Gamepad_Mapping.Utils.Theme;

namespace Gamepad_Mapping.Views;

public partial class ComboHudWindow : Window
{
    private byte _lastPanelAlpha = 140;
    private double _lastShadowOpacity = 0.28;

    public ComboHudWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => TopMostOverlayHelper.ApplyToWindow(this);
        App.ThemeChanged += OnAppThemeChanged;
        Closed += (_, _) => App.ThemeChanged -= OnAppThemeChanged;
    }

    private void OnAppThemeChanged(object? sender, EventArgs e) =>
        ApplyVisualSettings(_lastPanelAlpha, _lastShadowOpacity);

    /// <summary>Updates panel tint, border, and shadow from user settings (live while the HUD is open).</summary>
    public void ApplyVisualSettings(byte panelAlpha, double shadowOpacity)
    {
        _lastPanelAlpha = panelAlpha;
        _lastShadowOpacity = shadowOpacity;
        panelAlpha = (byte)Math.Clamp((int)panelAlpha, 24, 220);
        shadowOpacity = Math.Clamp(shadowOpacity, 0.08, 0.60);

        var chrome = App.UsesLightTheme ? AppChromeTheme.Light : AppChromeTheme.Dark;
        AppChromeTheme.ApplyHudChrome(App.UsesLightTheme, chrome, panelAlpha, out var panel, out var border);
        RootBorder.Background = new SolidColorBrush(panel);
        RootBorder.BorderBrush = new SolidColorBrush(border);

        if (RootBorder.Effect is DropShadowEffect dse)
            dse.Opacity = shadowOpacity;
    }

    public void ShowHud(ComboHudContent content, byte panelAlpha, double shadowOpacity, ComboHudPlacement placement)
    {
        if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            fadeIn.Stop();
        if (Resources["FadeOutStoryboard"] is Storyboard fadeOut)
        {
            fadeOut.Stop();
            fadeOut.Completed -= OnFadeOutCompleted;
        }

        ApplyVisualSettings(panelAlpha, shadowOpacity);
        ApplyHudLayoutBounds();
        TitleBlock.Text = content.Title;
        LinesItems.ItemsSource = content.Lines;
        var wasHidden = !IsVisible;
        if (wasHidden)
            RootBorder.Opacity = 0;
        else
            RootBorder.Opacity = 1;

        if (wasHidden)
            Show();

        void PositionAndMaybeFadeIn()
        {
            UpdateLayout();
            PositionHud(placement);
            if (wasHidden && Resources["FadeInStoryboard"] is Storyboard fi)
                fi.Begin(this);
        }

        if (wasHidden)
            Dispatcher.BeginInvoke(PositionAndMaybeFadeIn, DispatcherPriority.Loaded);
        else
            Dispatcher.BeginInvoke(PositionAndMaybeFadeIn, DispatcherPriority.Render);
    }

    public void HideHud()
    {
        if (!IsVisible)
            return;
        if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            fadeIn.Stop();
        if (Resources["FadeOutStoryboard"] is not Storyboard fadeOut)
        {
            Hide();
            return;
        }

        fadeOut.Stop();
        fadeOut.Completed -= OnFadeOutCompleted;
        fadeOut.Completed += OnFadeOutCompleted;
        fadeOut.Begin(this);
    }

    private void OnFadeOutCompleted(object? sender, EventArgs e)
    {
        if (Resources["FadeOutStoryboard"] is Storyboard fadeOut)
            fadeOut.Completed -= OnFadeOutCompleted;
        Hide();
        RootBorder.Opacity = 1;
    }

    /// <summary>
    /// Lets long tip lists scroll inside the work area instead of a fixed 320px cap or clipping off-screen.
    /// </summary>
    private void ApplyHudLayoutBounds()
    {
        var work = SystemParameters.WorkArea;
        const double screenEdgeMargin = 28;
        const double chromeAboveScroll = 102;
        var maxWindowH = Math.Max(200, work.Height - screenEdgeMargin);
        MaxHeight = maxWindowH;
        LinesScrollViewer.MaxHeight = Math.Max(220, maxWindowH - chromeAboveScroll);
    }

    private void PositionHud(ComboHudPlacement placement)
    {
        var area = SystemParameters.WorkArea;
        const double margin = 20;
        const double clampPadding = 12;
        var centerX = area.Left + (area.Width - ActualWidth) / 2;
        var centerY = area.Top + (area.Height - ActualHeight) / 2;

        (Left, Top) = placement switch
        {
            ComboHudPlacement.TopLeft => (area.Left + margin, area.Top + margin),
            ComboHudPlacement.TopRight => (area.Right - ActualWidth - margin, area.Top + margin),
            ComboHudPlacement.BottomLeft => (area.Left + margin, area.Bottom - ActualHeight - margin),
            ComboHudPlacement.Center => (centerX, centerY),
            _ => (area.Right - ActualWidth - margin, area.Bottom - ActualHeight - margin)
        };

        if (Left < area.Left)
            Left = area.Left + clampPadding;
        if (Top < area.Top)
            Top = area.Top + clampPadding;
    }
}
