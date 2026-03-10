using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LuminaLex.Converters;

public sealed class RectConverter : IMultiValueConverter
{
    public static readonly RectConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double w && values[1] is double h)
            return new Rect(0, 0, w, h);
        return new Rect();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
