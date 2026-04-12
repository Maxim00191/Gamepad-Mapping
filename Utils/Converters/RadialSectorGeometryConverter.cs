#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Gamepad_Mapping.Utils;

/// <summary>
/// MultiBinding: SegmentIndex, SegmentCount → annulus sector <see cref="Geometry"/> for HUD slice backgrounds.
/// </summary>
public class RadialSectorGeometryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not int segmentIndex ||
            values[1] is not int segmentCount)
            return Geometry.Empty;

        return RadialHudLayout.CreateAnnulusSectorGeometry(segmentIndex, segmentCount);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
