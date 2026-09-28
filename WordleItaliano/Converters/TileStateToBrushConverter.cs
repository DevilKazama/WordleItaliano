using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WordleItaliano.Models;

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
        var isOriginal = theme == "Original";
        var isIbpLight = theme == "IbpLight";

        if (isOriginal)
        {
            return state switch
            {
                TileState.Correct => new SolidColorBrush(Color.FromRgb(83, 141, 78)),
                TileState.Present => new SolidColorBrush(Color.FromRgb(181, 159, 59)),
                TileState.Absent => new SolidColorBrush(Color.FromRgb(120, 124, 126)),
                TileState.Filled => Brushes.White,
                _ => Brushes.White
            };
        }

        return state switch
        {
            TileState.Correct => new SolidColorBrush(Color.FromRgb(0, 116, 122)),
            TileState.Present => new SolidColorBrush(Color.FromRgb(194, 158, 61)),
            TileState.Absent => new SolidColorBrush(Color.FromRgb(162, 35, 39)),
            TileState.Filled => new SolidColorBrush(isIbpLight ? Colors.White : Color.FromRgb(43, 55, 60)),
            _ => new SolidColorBrush(isIbpLight ? Color.FromRgb(229, 238, 240) : Color.FromRgb(54, 67, 72))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

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
