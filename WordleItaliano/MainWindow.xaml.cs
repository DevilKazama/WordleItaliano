using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
            viewModel.PerfectShotAnimationRequested += (_, row) => CelebratePerfectShot(row);
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
                if (e.Key == Key.Enter && viewModel.IsPerfectShotPrizeDialogVisible &&
                    viewModel.ConfirmPerfectShotPrizeCommand.CanExecute(null))
                {
                    viewModel.ConfirmPerfectShotPrizeCommand.Execute(null);
                    e.Handled = true;
                    return;
                }

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

    private void CelebratePerfectShot(int row)
    {
        CelebrateRow(row);
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
                    BeginTime = TimeSpan.FromMilliseconds(columns * 110 + 260 + i * 55),
                    Duration = TimeSpan.FromMilliseconds(620)
                };
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.96, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(410))));
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620))));

                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
                transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
            }
        }
    }

    private void PerfectShotCelebrationOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (PerfectShotCelebrationOverlay.IsVisible)
        {
            BeginPerfectShotOverlayAnimation();
        }
    }

    private void BeginPerfectShotOverlayAnimation()
    {
        PerfectShotCelebrationCard.Opacity = 0;
        if (PerfectShotCelebrationCard.RenderTransform is ScaleTransform cardScale)
        {
            cardScale.ScaleX = 0.75;
            cardScale.ScaleY = 0.75;
            AnimateScale(cardScale, 0.75, 1.08, 1.0, 520);
        }

        PerfectShotCelebrationCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

        PerfectShotGlow.Opacity = 1.0;
        PerfectShotGlow.BlurRadius = 56;
        PerfectShotGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(1.0, 0.55, TimeSpan.FromMilliseconds(760))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        PerfectShotGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(56, 30, TimeSpan.FromMilliseconds(760))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

        if (PerfectShotCelebrationTitle.RenderTransform is ScaleTransform titleScale)
        {
            titleScale.ScaleX = 0.86;
            titleScale.ScaleY = 0.86;
            AnimateScale(titleScale, 0.86, 1.2, 1.0, 700);
        }

        BuildPerfectShotParticles();
    }

    private static void AnimateScale(ScaleTransform transform, double from, double overshoot, double to, int durationMilliseconds)
    {
        var scaleX = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(durationMilliseconds) };
        var scaleY = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(durationMilliseconds) };
        foreach (var animation in new[] { scaleX, scaleY })
        {
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(overshoot, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 }
            });
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMilliseconds)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    private void BuildPerfectShotParticles()
    {
        PerfectShotParticleCanvas.Children.Clear();
        var centerX = ActualWidth > 0 ? ActualWidth / 2 : 380;
        var centerY = ActualHeight > 0 ? ActualHeight / 2 : 390;
        var random = new Random(1701);
        var brushes = new Brush[]
        {
            new SolidColorBrush(Color.FromRgb(201, 180, 88)),
            new SolidColorBrush(Color.FromRgb(83, 141, 78)),
            new SolidColorBrush(Color.FromRgb(246, 232, 159)),
            new SolidColorBrush(Color.FromRgb(106, 170, 100))
        };

        for (var i = 0; i < 42; i++)
        {
            var isCircle = i % 3 == 0;
            var size = random.Next(6, 13);
            Shape particle = isCircle
                ? new Ellipse { Width = size, Height = size }
                : new Rectangle { Width = size + 3, Height = size, RadiusX = 1, RadiusY = 1 };
            particle.Fill = brushes[i % brushes.Length];
            particle.Opacity = 0;
            particle.RenderTransformOrigin = new Point(0.5, 0.5);
            var translate = new TranslateTransform();
            var rotate = new RotateTransform(random.Next(-25, 25));
            var transforms = new TransformGroup();
            transforms.Children.Add(rotate);
            transforms.Children.Add(translate);
            particle.RenderTransform = transforms;
            var startX = random.Next(-260, 261);
            var startY = random.Next(-105, 86);
            Canvas.SetLeft(particle, centerX + startX);
            Canvas.SetTop(particle, centerY + startY);
            PerfectShotParticleCanvas.Children.Add(particle);

            var begin = TimeSpan.FromMilliseconds(random.Next(80, 360));
            var side = startX < 0 ? -1 : 1;
            var driftX = side * random.Next(55, 130) + random.Next(-30, 31);
            var driftY = random.Next(-150, -65);
            var settleY = driftY + random.Next(65, 115);
            particle.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
            {
                BeginTime = begin,
                Duration = TimeSpan.FromMilliseconds(1600),
                KeyFrames =
                {
                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                    new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))),
                    new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(980))),
                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600)))
                }
            });
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, driftX, TimeSpan.FromMilliseconds(1300))
            {
                BeginTime = begin,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimationUsingKeyFrames
            {
                BeginTime = begin,
                Duration = TimeSpan.FromMilliseconds(1600),
                KeyFrames =
                {
                    new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                    new EasingDoubleKeyFrame(driftY, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(540)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    },
                    new EasingDoubleKeyFrame(settleY, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    }
                }
            });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rotate.Angle, rotate.Angle + random.Next(-220, 221), TimeSpan.FromMilliseconds(1500))
            {
                BeginTime = begin
            });
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



