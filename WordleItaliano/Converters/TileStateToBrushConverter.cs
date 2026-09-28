using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TileState.Correct => new SolidColorBrush(Color.FromRgb(0, 116, 122)),
            TileState.Present => new SolidColorBrush(Color.FromRgb(194, 158, 61)),
            TileState.Absent => new SolidColorBrush(Color.FromRgb(78, 86, 90)),
            TileState.Filled => new SolidColorBrush(Color.FromRgb(43, 55, 60)),
            _ => new SolidColorBrush(Color.FromRgb(54, 67, 72))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
