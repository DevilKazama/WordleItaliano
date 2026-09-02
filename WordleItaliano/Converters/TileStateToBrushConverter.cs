using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var dark = parameter?.ToString() == "Dark";
        return value switch
        {
            TileState.Correct => new SolidColorBrush(Color.FromRgb(83, 141, 78)),
            TileState.Present => new SolidColorBrush(Color.FromRgb(181, 159, 59)),
            TileState.Absent => new SolidColorBrush(dark ? Color.FromRgb(58, 58, 60) : Color.FromRgb(120, 124, 126)),
            TileState.Filled => new SolidColorBrush(dark ? Color.FromRgb(18, 18, 19) : Colors.White),
            _ => new SolidColorBrush(dark ? Color.FromRgb(18, 18, 19) : Colors.White)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
