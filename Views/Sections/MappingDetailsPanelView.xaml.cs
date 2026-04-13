using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Gamepad_Mapping.Views.Sections;

public partial class MappingDetailsPanelView : UserControl
{
    public MappingDetailsPanelView()
    {
        InitializeComponent();
    }

    public void TryFocusFirstEditorField()
    {
        Dispatcher.BeginInvoke(() =>
        {
            MappingDetailsEditorScroll?.BringIntoView();
            foreach (var d in EnumerateVisualDescendants(this))
            {
                if (d is not FrameworkElement fe || !fe.IsVisible || !fe.IsEnabled)
                    continue;
                if (d is ComboBox cb)
                {
                    cb.Focus();
                    return;
                }

                if (d is TextBox tb && !tb.IsReadOnly)
                {
                    tb.Focus();
                    return;
                }
            }
        }, DispatcherPriority.Input);
    }

    private static IEnumerable<DependencyObject> EnumerateVisualDescendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in EnumerateVisualDescendants(child))
                yield return nested;
        }
    }
}
