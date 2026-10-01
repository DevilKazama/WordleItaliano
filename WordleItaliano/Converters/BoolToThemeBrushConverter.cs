using System.Globalization;
using System.Windows.Data;
using WordleItaliano.Theme;

namespace WordleItaliano.Converters;

public sealed class BoolToThemeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var theme = value is true ? ThemeModes.IbpDark : value?.ToString();
        return ThemePalette.For(theme).GetThemeBrush(parameter?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
