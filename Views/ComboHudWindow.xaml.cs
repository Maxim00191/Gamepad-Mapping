using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GamepadMapperGUI.Models;
using Gamepad_Mapping.Utils;

namespace Gamepad_Mapping.Views;

public partial class ComboHudWindow : Window
{
    public ComboHudWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => TopMostOverlayHelper.ApplyToWindow(this);
    }

    public void ShowHud(ComboHudContent content)
    {
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
