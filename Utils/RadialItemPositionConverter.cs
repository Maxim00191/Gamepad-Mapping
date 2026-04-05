using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// MultiBinding: SegmentIndex, SegmentCount → Canvas X or Y (top-left of item slot) using <see cref="RadialHudLayout"/>.
/// </summary>
public class RadialItemPositionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not int segmentIndex ||
            values[1] is not int segmentCount)
            return 0.0;

        var topLeft = RadialHudLayout.ItemSlotTopLeft(segmentIndex, segmentCount);
        var axis = parameter as string ?? "X";
        return axis.Equals("Y", StringComparison.OrdinalIgnoreCase) ? topLeft.Y : topLeft.X;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
