using System.Windows;

namespace Gamepad_Mapping.Behaviors;

public static class ButtonAttentionAssist
{
    public static readonly DependencyProperty IsSoftWarningHighlightedProperty =
        DependencyProperty.RegisterAttached(
            "IsSoftWarningHighlighted",
            typeof(bool),
            typeof(ButtonAttentionAssist),
            new FrameworkPropertyMetadata(false));

    public static void SetIsSoftWarningHighlighted(DependencyObject element, bool value) =>
        element.SetValue(IsSoftWarningHighlightedProperty, value);

    public static bool GetIsSoftWarningHighlighted(DependencyObject element) =>
        element.GetValue(IsSoftWarningHighlightedProperty) is bool value && value;
}
