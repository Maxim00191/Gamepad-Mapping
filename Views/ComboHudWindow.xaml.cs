using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.Utils;

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

        if (App.UsesLightTheme)
        {
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(panelAlpha, 0xFC, 0xFC, 0xFE));
            var borderA = (byte)Math.Clamp(panelAlpha / 3 + 24, 44, 100);
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(borderA, 0x28, 0x28, 0x34));
        }
        else
        {
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(panelAlpha, 0x1C, 0x1C, 0x1E));
            var borderA = (byte)Math.Clamp(panelAlpha / 2 + 28, 32, 100);
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(borderA, 0, 0, 0));
        }

        if (RootBorder.Effect is DropShadowEffect dse)
            dse.Opacity = shadowOpacity;
    }

    public void ShowHud(ComboHudContent content, byte panelAlpha, double shadowOpacity)
    {
        ApplyVisualSettings(panelAlpha, shadowOpacity);
        ApplyHudLayoutBounds();
        TitleBlock.Text = content.Title;
        LinesItems.ItemsSource = content.Lines;
        var wasHidden = !IsVisible;
        if (wasHidden)
            RootBorder.Opacity = 0;
        if (wasHidden)
            Show();
        Dispatcher.BeginInvoke(() =>
        {
            PositionBottomRight();
            if (wasHidden && Resources["FadeInStoryboard"] is Storyboard fadeIn)
                fadeIn.Begin(this);
        }, DispatcherPriority.Loaded);
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

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 20;
        Top = area.Bottom - ActualHeight - 20;
        if (Left < area.Left)
            Left = area.Left + 12;
        if (Top < area.Top)
            Top = area.Top + 12;
    }
}
