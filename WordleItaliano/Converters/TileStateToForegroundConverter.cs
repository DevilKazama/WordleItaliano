using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

namespace WordleItaliano.Converters;

public sealed class TileStateToForegroundConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (state, theme) = ReadStateAndTheme(value, parameter);
        return ConvertCore(state, theme);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var (state, theme) = ReadStateAndTheme(values, parameter);
        return ConvertCore(state, theme);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static object ConvertCore(TileState state, string theme)
    {
        if (theme == "Original" && state is TileState.Empty or TileState.Filled)
        {
            return new SolidColorBrush(Color.FromRgb(18, 18, 19));
        }

        if (theme == "IbpLight" && state is TileState.Empty or TileState.Filled)
        {
            return new SolidColorBrush(Color.FromRgb(18, 32, 36));
        }

        return Brushes.White;
    }

    private static (TileState State, string Theme) ReadStateAndTheme(object value, object? parameter)
    {
        if (value is object[] values)
        {
            var state = values.ElementAtOrDefault(0) is TileState multiState ? multiState : TileState.Empty;
            var theme = values.ElementAtOrDefault(1)?.ToString() ?? "IbpDark";
            return (state, theme);
        }

        var singleState = value is TileState tileState ? tileState : TileState.Empty;
        return (singleState, parameter?.ToString() ?? "IbpDark");
    }
}
