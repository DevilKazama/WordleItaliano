using System.Globalization;
using System.Windows.Data;
using WordleItaliano.Models;
using WordleItaliano.Theme;

namespace WordleItaliano.Converters;

public sealed class TileStateToBrushConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var (state, theme) = ReadStateAndTheme(value, parameter);
        return ConvertCore(state, theme);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var (state, theme) = ReadStateAndTheme(values, parameter);
        return ConvertCore(state, theme);
    }

    private static object ConvertCore(TileState state, string theme)
    {
        return ThemePalette.For(theme).GetTileBrush(state);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static (TileState State, string Theme) ReadStateAndTheme(object value, object? parameter)
    {
        if (value is object[] values)
        {
            var state = values.ElementAtOrDefault(0) is TileState multiState ? multiState : TileState.Empty;
            var theme = values.ElementAtOrDefault(1)?.ToString() ?? ThemeModes.IbpDark;
            return (state, theme);
        }

        var singleState = value is TileState tileState ? tileState : TileState.Empty;
        return (singleState, parameter?.ToString() ?? ThemeModes.IbpDark);
    }
}
