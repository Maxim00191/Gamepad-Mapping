using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Gamepad_Mapping.UI;

public static class ComboBoxMouseWheelBehavior
{
    public static readonly DependencyProperty DisableMouseWheelSelectionProperty =
        DependencyProperty.RegisterAttached(
            "DisableMouseWheelSelection",
            typeof(bool),
            typeof(ComboBoxMouseWheelBehavior),
            new PropertyMetadata(false, OnDisableMouseWheelSelectionChanged));

    public static void SetDisableMouseWheelSelection(DependencyObject element, bool value)
        => element.SetValue(DisableMouseWheelSelectionProperty, value);

    public static bool GetDisableMouseWheelSelection(DependencyObject element)
        => (bool)element.GetValue(DisableMouseWheelSelectionProperty);

    private static void OnDisableMouseWheelSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox comboBox)
            return;

        if ((bool)e.NewValue)
            comboBox.PreviewMouseWheel += ComboBoxOnPreviewMouseWheel;
        else
            comboBox.PreviewMouseWheel -= ComboBoxOnPreviewMouseWheel;
    }

    private static void ComboBoxOnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        // Allow wheel navigation only when the dropdown is intentionally open.
        if (comboBox.IsDropDownOpen)
            return;

        e.Handled = true;

        var parent = FindVisualParent<UIElement>(comboBox);
        if (parent is null)
            return;

        var parentWheel = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = comboBox
        };
        parent.RaiseEvent(parentWheel);
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T typed)
                return typed;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
