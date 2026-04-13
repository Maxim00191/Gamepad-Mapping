using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Gamepad_Mapping.ViewModels;

namespace Gamepad_Mapping.Views.Sections;

public partial class ControllerVisualView : UserControl
{
    public ControllerVisualView()
    {
        InitializeComponent();
    }

    private void Path_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is ControllerVisualViewModel vm)
        {
            vm.SelectElementCommand.Execute(element.Tag as string);
        }
    }

    private void Path_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && DataContext is ControllerVisualViewModel vm)
        {
            vm.HoverElementCommand.Execute(element.Tag as string);
        }
    }

    private void Path_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is ControllerVisualViewModel vm)
        {
            vm.HoverElementCommand.Execute(null);
        }
    }
}
