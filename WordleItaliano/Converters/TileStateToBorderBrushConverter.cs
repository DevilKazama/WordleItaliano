using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TileState.Empty => new SolidColorBrush(Color.FromRgb(88, 106, 113)),
            TileState.Filled => new SolidColorBrush(Color.FromRgb(0, 116, 122)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
