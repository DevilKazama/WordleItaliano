using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var dark = parameter?.ToString() == "Dark";
        return value is TileState.Correct or TileState.Present or TileState.Absent
            ? Brushes.White
            : new SolidColorBrush(dark ? Colors.White : Color.FromRgb(18, 18, 19));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
