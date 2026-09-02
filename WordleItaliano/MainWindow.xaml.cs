using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WordleItaliano.ViewModels;

namespace WordleItaliano;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _dateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _timerRefresh = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ShakeRequested += (_, row) => ShakeRow(row);
            viewModel.RevealRequested += (_, row) => RevealRow(row);
            viewModel.LetterEntered += (_, index) => PopTile(index);
            viewModel.VictoryAnimationRequested += (_, row) => CelebrateRow(row);
            viewModel.DefeatAnimationRequested += (_, row) => DefeatPulseRow(row);
            Activated += (_, _) => viewModel.EnsureCurrentGame();
            _dateTimer.Tick += (_, _) => viewModel.EnsureCurrentGame();
            _dateTimer.Start();
            _timerRefresh.Tick += (_, _) => viewModel.TickTimer();
            _timerRefresh.Start();
            Closing += (_, _) => viewModel.PersistActiveGameTime();
            viewModel.EnsureCurrentGame();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            if (IsTextInputTarget(e.OriginalSource))
            {
                if (e.Key == Key.Enter && viewModel.SaveProfileCommand.CanExecute(null))
                {
                    viewModel.SaveProfileCommand.Execute(null);
                    e.Handled = true;
                }

                return;
            }

            viewModel.HandlePhysicalKey(e.Key);
            if (e.Key is Key.Enter or Key.Back or Key.Left or Key.Right or >= Key.A and <= Key.Z)
            {
                e.Handled = true;
            }
        }
    }

    private static bool IsTextInputTarget(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is TextBox)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Tile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is FrameworkElement { DataContext: TileViewModel tile })
        {
            viewModel.SelectTile(tile.Index);
            e.Handled = true;
        }
    }

    private void ShakeRow(int row)
    {
        var columns = GetBoardColumns();
        for (var i = 0; i < columns; i++)
        {
            if (Board.ItemContainerGenerator.ContainerFromIndex(row * columns + i) is ContentPresenter presenter)
            {
                var transform = new System.Windows.Media.TranslateTransform();
                presenter.RenderTransform = transform;
                var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(330) };
                foreach (var frame in new[] { 0, -8, 8, -6, 6, -3, 3, 0 })
                {
                    animation.KeyFrames.Add(new LinearDoubleKeyFrame(frame, KeyTime.Uniform));
                }

                transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
            }
        }
    }

    private void RevealRow(int row)
    {
        var columns = GetBoardColumns();
        for (var i = 0; i < columns; i++)
        {
            if (Board.ItemContainerGenerator.ContainerFromIndex(row * columns + i) is ContentPresenter presenter)
            {
                presenter.RenderTransformOrigin = new Point(0.5, 0.5);
                var transform = new System.Windows.Media.ScaleTransform(1, 1);
                presenter.RenderTransform = transform;

                var collapse = new DoubleAnimation(1, 0.04, TimeSpan.FromMilliseconds(120))
                {
                    BeginTime = TimeSpan.FromMilliseconds(i * 110)
                };
                var expand = new DoubleAnimation(0.04, 1, TimeSpan.FromMilliseconds(140))
                {
                    BeginTime = TimeSpan.FromMilliseconds(i * 110 + 120)
                };

                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, collapse);
                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, expand);
            }
        }
    }

    private void PopTile(int index)
    {
        if (Board.ItemContainerGenerator.ContainerFromIndex(index) is not ContentPresenter presenter)
        {
            return;
        }

        presenter.RenderTransformOrigin = new Point(0.5, 0.5);
        var transform = new System.Windows.Media.ScaleTransform(1, 1);
        presenter.RenderTransform = transform;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(160) };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));

        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
    }

    private void CelebrateRow(int row)
    {
        var columns = GetBoardColumns();
        for (var i = 0; i < columns; i++)
        {
            if (Board.ItemContainerGenerator.ContainerFromIndex(row * columns + i) is ContentPresenter presenter)
            {
                presenter.RenderTransformOrigin = new Point(0.5, 0.5);
                var transform = new System.Windows.Media.ScaleTransform(1, 1);
                presenter.RenderTransform = transform;
                var animation = new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = TimeSpan.FromMilliseconds(columns * 110 + 180 + i * 70),
                    Duration = TimeSpan.FromMilliseconds(220)
                };
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));

                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
            }
        }
    }

    private void DefeatPulseRow(int row)
    {
        var columns = GetBoardColumns();
        for (var i = 0; i < columns; i++)
        {
            if (Board.ItemContainerGenerator.ContainerFromIndex(row * columns + i) is ContentPresenter presenter)
            {
                presenter.RenderTransformOrigin = new Point(0.5, 0.5);
                var transform = new System.Windows.Media.ScaleTransform(1, 1);
                presenter.RenderTransform = transform;
                var animation = new DoubleAnimationUsingKeyFrames
                {
                    BeginTime = TimeSpan.FromMilliseconds(columns * 110 + 160),
                    Duration = TimeSpan.FromMilliseconds(240)
                };
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.96, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));

                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
            }
        }

        ShakeRow(row);
    }

    private int GetBoardColumns()
    {
        return DataContext is MainViewModel viewModel ? viewModel.BoardColumns : 5;
    }
}
