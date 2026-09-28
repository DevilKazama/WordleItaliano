using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WordleItaliano.Converters;

public sealed class BoolToThemeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var theme = value?.ToString();
        var isIbpDark = theme == "IbpDark" || value is true;
        var isIbpLight = theme == "IbpLight";
        var isOriginal = theme == "Original";

        if (isOriginal)
        {
            return parameter?.ToString() switch
            {
                "Background" => new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                "Foreground" => new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                "Panel" => Brushes.White,
                "Border" => new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                "Muted" => new SolidColorBrush(Color.FromRgb(92, 92, 96)),
                _ => Brushes.Transparent
            };
        }

        return parameter?.ToString() switch
        {
            "Background" => new SolidColorBrush(isIbpDark ? Color.FromRgb(30, 38, 42) : Color.FromRgb(243, 247, 248)),
            "Foreground" => new SolidColorBrush(isIbpDark ? Color.FromRgb(247, 250, 250) : Color.FromRgb(37, 48, 52)),
            "Panel" => new SolidColorBrush(isIbpDark ? Color.FromRgb(38, 48, 52) : Colors.White),
            "Border" => new SolidColorBrush(isIbpDark ? Color.FromRgb(76, 92, 98) : Color.FromRgb(201, 211, 214)),
            "Muted" => new SolidColorBrush(isIbpDark ? Color.FromRgb(198, 209, 212) : Color.FromRgb(91, 105, 110)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
