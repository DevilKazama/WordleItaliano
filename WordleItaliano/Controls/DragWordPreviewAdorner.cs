using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WordleItaliano.Theme;
using WordleItaliano.ViewModels;

namespace WordleItaliano.Controls;

internal sealed class DragWordPreviewAdorner(UIElement surface, MainViewModel vm, string word) : Adorner(surface)
{
    public Point Position { get; set; }
    protected override void OnRender(DrawingContext context)
    {
        var palette = ThemePalette.For(vm.ThemeMode);
        var text = new FormattedText(word.ToUpperInvariant(), CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, palette.Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var x = Math.Clamp(Position.X + 18, 6, Math.Max(6, AdornedElement.RenderSize.Width - text.Width - 18));
        var y = Math.Clamp(Position.Y + 18, 6, Math.Max(6, AdornedElement.RenderSize.Height - text.Height - 14));
        context.PushOpacity(0.9);
        context.DrawRoundedRectangle(palette.Panel, new Pen(palette.Accent, 1),
            new Rect(x - 6, y - 4, text.Width + 12, text.Height + 8), 3, 3);
        context.DrawText(text, new Point(x, y));
        context.Pop();
    }
}
