using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Gamepad_Mapping.Utils;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views;

public partial class RadialMenuHudWindow : Window
{
    private readonly RadialMenuHudViewModel _viewModel;

    public RadialMenuHudWindow()
    {
        InitializeComponent();
        _viewModel = new RadialMenuHudViewModel();
        DataContext = _viewModel;

        SourceInitialized += (_, _) => TopMostOverlayHelper.ApplyToWindow(this);
    }

    public void ShowMenu(string title, IEnumerable<RadialMenuItemViewModel> items)
    {
        if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            fadeIn.Stop();
        if (Resources["FadeOutStoryboard"] is Storyboard fadeOut)
        {
            fadeOut.Stop();
            fadeOut.Completed -= OnFadeOutCompleted;
        }

        _viewModel.Title = title;
        _viewModel.Items.Clear();
        foreach (var item in items)
            _viewModel.Items.Add(item);

        var wasHidden = !IsVisible;
        if (wasHidden)
        {
            RootGrid.Opacity = 0;
            ScaleTransform.ScaleX = 0.8;
            ScaleTransform.ScaleY = 0.8;
            Show();
        }
        else
        {
            RootGrid.Opacity = 1;
            ScaleTransform.ScaleX = 1;
            ScaleTransform.ScaleY = 1;
        }

        void PositionAndMaybeFadeIn()
        {
            UpdateLayout();
            PositionAtCenter();
            if (wasHidden && Resources["FadeInStoryboard"] is Storyboard fi)
                fi.Begin(this);
        }

        if (wasHidden)
            Dispatcher.BeginInvoke(PositionAndMaybeFadeIn, DispatcherPriority.Loaded);
        else
            Dispatcher.BeginInvoke(PositionAndMaybeFadeIn, DispatcherPriority.Render);
    }

    public void HideMenu()
    {
        if (!IsVisible)
            return;

        if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
            fadeIn.Stop();
        if (Resources["FadeOutStoryboard"] is not Storyboard fadeOut)
        {
            Hide();
            _viewModel.Items.Clear();
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
        RootGrid.Opacity = 1;
        ScaleTransform.ScaleX = 0.8;
        ScaleTransform.ScaleY = 0.8;
        _viewModel.Items.Clear();
    }

    private void PositionAtCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Top + (area.Height - ActualHeight) / 2;
    }

    public void UpdateSelection(int index)
    {
        _viewModel.SelectedIndex = index;
        for (int i = 0; i < _viewModel.Items.Count; i++)
        {
            _viewModel.Items[i].IsSelected = (i == index);
        }
    }
}
