using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WordleItaliano.Converters;

public sealed class BoolToThemeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isDark = value is true;
        return parameter?.ToString() switch
        {
            "Background" => new SolidColorBrush(isDark ? Color.FromRgb(18, 18, 19) : Color.FromRgb(248, 248, 248)),
            "Foreground" => new SolidColorBrush(isDark ? Color.FromRgb(245, 245, 245) : Color.FromRgb(28, 28, 30)),
            "Panel" => new SolidColorBrush(isDark ? Color.FromRgb(28, 28, 30) : Colors.White),
            "Border" => new SolidColorBrush(isDark ? Color.FromRgb(58, 58, 60) : Color.FromRgb(225, 225, 225)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
