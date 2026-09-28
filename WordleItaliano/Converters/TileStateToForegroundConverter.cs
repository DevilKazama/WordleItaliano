using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
