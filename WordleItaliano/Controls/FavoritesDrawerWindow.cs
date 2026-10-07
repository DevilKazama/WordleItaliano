using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WordleItaliano.ViewModels;

namespace WordleItaliano.Controls;

// An owned tool window stays in the owner's application group without being topmost.
internal sealed class FavoritesDrawerWindow : Window
{
    private readonly Window _main;
    private readonly TranslateTransform _slide = new();
    private readonly Grid _surface;
    private bool _right;
    private bool _retracting;
    private int _animationGeneration;

    public FavoritesDrawerWindow(Window main, MainViewModel vm)
    {
        _main = main;
        Owner = main;
        DataContext = vm;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        SizeToContent = SizeToContent.Height;
        Width = 276;
        _surface = new Grid { ClipToBounds = true };
        _surface.Children.Add(new FavoritesPanel { RenderTransform = _slide });
        Content = _surface;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x80);
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(Attach));
        SizeChanged += (_, _) => Attach();
        Closed += (_, _) =>
        {
            _animationGeneration++;
            _slide.BeginAnimation(TranslateTransform.XProperty, null);
            Content = null;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (vm.IsManualFavoriteOpen)
            {
                if (e.Key == System.Windows.Input.Key.Escape) { vm.CancelFavoriteForm(); e.Handled = true; }
                else if (e.Key == System.Windows.Input.Key.Enter) { vm.ConfirmManualFavoriteCommand.Execute(null); e.Handled = true; }
                return;
            }
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                vm.IsFavoritesPanelOpen = false;
                e.Handled = true;
            }
        };
    }

    public void OpenAttached(bool right)
    {
        var animate = !IsVisible || _retracting || _right != right;
        _right = right;
        _retracting = false;
        _animationGeneration++;
        if (!IsVisible) Show();
        Attach();
        if (animate)
            _slide.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(right ? -Width : Width, 0, TimeSpan.FromMilliseconds(150)));
    }
    public void ClearSubmittedDropFeedback()
    {
        if (_surface.Children[0] is FavoritesPanel panel) panel.RestoreDropBorder();
    }

    public void Retract(bool immediate)
    {
        if (!IsVisible || _retracting) { if (immediate) Hide(); return; }
        var generation = ++_animationGeneration;
        if (immediate) { Hide(); return; }
        _retracting = true;
        var animation = new DoubleAnimation(0, _right ? -Width : Width, TimeSpan.FromMilliseconds(120));
        animation.Completed += (_, _) =>
        {
            if (generation != _animationGeneration) return;
            Hide();
            _retracting = false;
        };
        _slide.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void Attach()
    {
        if (!IsVisible || _main.WindowState == WindowState.Minimized) return;
        var ownerHandle = new WindowInteropHelper(_main).Handle;
        var handle = new WindowInteropHelper(this).Handle;
        if (ownerHandle == IntPtr.Zero || handle == IntPtr.Zero || !GetWindowRect(ownerHandle, out var owner)) return;
        // DWM excludes the invisible resize frame. All geometry below is in screen pixels.
        if (DwmGetWindowAttribute(ownerHandle, 9, out var visibleOwner, Marshal.SizeOf<Rect>()) == 0)
            owner = visibleOwner;
        if (!GetWindowRect(handle, out var drawer) ||
            _main.Content is not FrameworkElement content) return;
        var areaTop = content.PointToScreen(new Point(0, 0)).Y;
        var areaBottom = content.PointToScreen(new Point(0, content.ActualHeight)).Y;
        var contentLeft = content.PointToScreen(new Point(0, 0)).X;
        var contentRight = content.PointToScreen(new Point(content.ActualWidth, 0)).X;
        var drawerLeftInset = _surface.PointToScreen(new Point(0, 0)).X - drawer.Left;
        var drawerRightInset = drawer.Right - _surface.PointToScreen(new Point(_surface.ActualWidth, 0)).X;
        var position = CalculateAttachment(contentLeft + drawerRightInset, contentRight - drawerLeftInset, areaTop, areaBottom,
            drawer.Right - drawer.Left, drawer.Bottom - drawer.Top, _right);
        SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(position.X), (int)Math.Round(position.Y), 0, 0, 0x0015);
    }

    internal static Point CalculateAttachment(double left, double right, double top, double bottom,
        double width, double height, bool opensRight) =>
        new(opensRight ? right : left - width, (top + bottom - height) / 2);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
}
