using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var dark = parameter?.ToString() == "Dark";
        return value switch
        {
            TileState.Empty => new SolidColorBrush(dark ? Color.FromRgb(58, 58, 60) : Color.FromRgb(211, 214, 218)),
            TileState.Filled => new SolidColorBrush(dark ? Color.FromRgb(86, 87, 88) : Color.FromRgb(135, 138, 140)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
