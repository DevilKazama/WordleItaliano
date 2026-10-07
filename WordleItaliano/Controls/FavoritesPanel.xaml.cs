using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using WordleItaliano.Theme;
using WordleItaliano.ViewModels;

namespace WordleItaliano.Controls;

public partial class FavoritesPanel : UserControl
{
    private Point _dragStart;
    private void SubmittedWord_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("WordleSubmittedWord")) return;
        SubmittedDropTarget.Visibility = Visibility.Visible;
        SubmittedDropWord.Text = (e.Data.GetData("WordleSubmittedWord") as string)?.ToUpperInvariant();
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }
    private void SubmittedWord_DragLeave(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("WordleSubmittedWord")) return;
        var position = e.GetPosition(PanelFrame);
        if (!new Rect(PanelFrame.RenderSize).Contains(position)) RestoreDropBorder();
    }
    private void SubmittedWord_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("WordleSubmittedWord")) return;
        if (DataContext is MainViewModel vm && e.Data.GetData("WordleSubmittedWord") is string word)
            e.Effects = vm.SaveSubmittedFavorite(word) ? DragDropEffects.Copy : DragDropEffects.None;
        RestoreDropBorder();
        e.Handled = true;
    }
    internal void RestoreDropBorder()
    {
        SubmittedDropTarget.Visibility = Visibility.Collapsed;
        PanelFrame.BorderThickness = new Thickness(1);
        PanelFrame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("ThemeMode")
        { Converter = new WordleItaliano.Converters.BoolToThemeBrushConverter(), ConverterParameter = "Border" });
    }
    private string? _dragWord;
    private string? _activeDragWord;
    private int _dropSlot = -1;
    private Point _dragPosition;
    private UIElement? _dragRow;
    private InsertionAdorner? _indicator;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    public FavoritesPanel()
    {
        InitializeComponent();
        _scrollTimer.Tick += (_, _) => AutoScroll();
        QueryContinueDrag += (_, e) =>
        {
            if (e.EscapePressed) { e.Action = DragAction.Cancel; ClearIndicator(); }
        };
        Unloaded += (_, _) => FinishDrag();
    }
    private void ManualFavoriteForm_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (ManualFavoriteForm.IsVisible) { ManualWordInput.Focus(); Keyboard.Focus(ManualWordInput); }
            }));
    }
    private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragWord = (sender as FrameworkElement)?.DataContext is FavoriteWordViewModel word ? word.Word : null;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }
    private void Handle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragWord is null) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var word = _dragWord;
        _dragWord = null;
        ((UIElement)sender).ReleaseMouseCapture();
        _activeDragWord = word;
        _dragRow = FindRow((DependencyObject)sender);
        if (_dragRow is not null) _dragRow.Opacity = 0.45;
        try { DragDrop.DoDragDrop((DependencyObject)sender, new DataObject("WordleFavorite", word), DragDropEffects.Move); }
        finally { FinishDrag(); }
        e.Handled = true;
    }
    private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragWord = null;
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }
    private void Words_DragOver(object sender, DragEventArgs e)
    {
        _dragPosition = e.GetPosition(WordsScroll);
        if (_activeDragWord is null || e.Data.GetData("WordleFavorite") is not string word || word != _activeDragWord || !IsInsideList(_dragPosition))
        { ClearIndicator(); e.Effects = DragDropEffects.None; }
        else
        { UpdateIndicator(); _scrollTimer.Start(); e.Effects = DragDropEffects.Move; }
        e.Handled = true;
    }
    private void Words_DragLeave(object sender, DragEventArgs e)
    {
        if (!IsInsideList(e.GetPosition(WordsScroll))) ClearIndicator();
        e.Handled = true;
    }
    private void Words_Drop(object sender, DragEventArgs e)
    {
        _dragPosition = e.GetPosition(WordsScroll);
        if (IsInsideList(_dragPosition) && _activeDragWord is not null && e.Data.GetData("WordleFavorite") is string source && source == _activeDragWord && DataContext is MainViewModel vm)
        {
            UpdateIndicator();
            if (_dropSlot >= 0) vm.MoveFavoriteToSlot(source, _dropSlot);
            e.Effects = DragDropEffects.Move;
        }
        else e.Effects = DragDropEffects.None;
        FinishDrag();
        e.Handled = true;
    }
    private bool IsInsideList(Point point) => point.X >= 0 && point.X < WordsScroll.ActualWidth - (WordsScroll.ScrollableHeight > 0 ? 12 : 0) &&
        point.Y >= 0 && point.Y <= WordsScroll.ActualHeight;
    private void UpdateIndicator()
    {
        _dropSlot = WordsItems.Items.Count;
        double lineY = 3;
        for (var index = 0; index < WordsItems.Items.Count; index++)
        {
            if (WordsItems.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement row) continue;
            var bounds = row.TransformToAncestor(WordsScroll).TransformBounds(new Rect(row.RenderSize));
            if (_dragPosition.Y < bounds.Top + bounds.Height / 2)
            { _dropSlot = index; lineY = bounds.Top; break; }
            lineY = bounds.Bottom;
        }
        var layer = AdornerLayer.GetAdornerLayer(WordsScroll);
        if (layer is null) return;
        if (_indicator is null) { _indicator = new InsertionAdorner(WordsScroll, () => ThemePalette.For((DataContext as MainViewModel)?.ThemeMode).Accent) { IsHitTestVisible = false }; layer.Add(_indicator); }
        _indicator.LineY = Math.Clamp(lineY, 2, Math.Max(2, WordsScroll.ActualHeight - 2));
        _indicator.InvalidateVisual();
    }
    private void AutoScroll()
    {
        if (_activeDragWord is null || !IsInsideList(_dragPosition)) { ClearIndicator(); return; }
        if (_dragPosition.Y < 28) WordsScroll.ScrollToVerticalOffset(WordsScroll.VerticalOffset - 12);
        else if (_dragPosition.Y > WordsScroll.ActualHeight - 28) WordsScroll.ScrollToVerticalOffset(WordsScroll.VerticalOffset + 12);
        else return;
        WordsScroll.UpdateLayout();
        UpdateIndicator();
    }
    private void ClearIndicator()
    {
        _scrollTimer.Stop();
        _dropSlot = -1;
        if (_indicator is not null) AdornerLayer.GetAdornerLayer(WordsScroll)?.Remove(_indicator);
        _indicator = null;
    }
    private void FinishDrag()
    {
        ClearIndicator();
        if (_dragRow is not null) _dragRow.Opacity = 1;
        _dragRow = null;
        _activeDragWord = null;
        _dragWord = null;
    }
    private static UIElement? FindRow(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Name: "FavoriteRow" } row) return row;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }
    private sealed class InsertionAdorner(UIElement element, Func<Brush> brush) : Adorner(element)
    {
        public double LineY { get; set; }
        protected override void OnRender(DrawingContext context)
        {
            context.DrawLine(new Pen(brush(), 3), new Point(2, LineY), new Point(Math.Max(2, AdornedElement.RenderSize.Width - 14), LineY));
        }
    }
}
