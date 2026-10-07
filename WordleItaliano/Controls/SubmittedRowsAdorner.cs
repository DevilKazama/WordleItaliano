using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WordleItaliano.Theme;
using WordleItaliano.ViewModels;

namespace WordleItaliano.Controls;

internal sealed class SubmittedRowsAdorner(UIElement board, MainViewModel vm) : Adorner(board)
{
    private Point _start;
    private string? _word;
    private Rect Handle(int row) => new(-30, row * AdornedElement.RenderSize.Height / 6 + (AdornedElement.RenderSize.Height / 6 - 30) / 2, 26, 30);
    public event Action<string>? DragRequested;
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
    {
        for (var row = 0; row < 6; row++)
            if (vm.GetSubmittedFavoriteWord(row * vm.BoardColumns) is not null && Handle(row).Contains(parameters.HitPoint))
                return new PointHitTestResult(this, parameters.HitPoint);
        return null;
    }
    protected override void OnRender(DrawingContext context)
    {
        var palette = ThemePalette.For(vm.ThemeMode);
        for (var row = 0; row < 6; row++)
        {
            if (vm.GetSubmittedFavoriteWord(row * vm.BoardColumns) is null) continue;
            var rect = Handle(row);
            context.DrawRoundedRectangle(IsMouseOver ? palette.CalendarHover : palette.Panel, null, rect, 3, 3);
            context.DrawText(Text("⋮⋮", palette.Muted), new Point(rect.Left + 5, rect.Top + 4));
        }
    }
    private FormattedText Text(string value, Brush brush) => new(value, CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        for (var row = 0; row < 6; row++)
            if (Handle(row).Contains(_start)) _word = vm.GetSubmittedFavoriteWord(row * vm.BoardColumns);
        if (_word is not null) CaptureMouse();
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        Cursor = Cursors.SizeAll;
        ToolTip = "Trascina nei Preferiti";
        InvalidateVisual();
        var point = e.GetPosition(this);
        if (_word is null || e.LeftButton != MouseButtonState.Pressed ||
            (Math.Abs(point.X - _start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - _start.Y) < SystemParameters.MinimumVerticalDragDistance)) return;
        var word = _word; _word = null; ReleaseMouseCapture(); DragRequested?.Invoke(word);
        e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { _word = null; ReleaseMouseCapture(); e.Handled = true; }
}
