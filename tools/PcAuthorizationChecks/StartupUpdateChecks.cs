using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Velopack;
using WordleItaliano.Services;
using WordleItaliano.ViewModels;

internal static class StartupUpdateChecks
{
    internal static void Run(string root, string approvedCode)
    {
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        foreach (var outcome in new[] { "available", "none", "failure" })
        foreach (var early in new[] { true, false })
        {
            var folder = Path.Combine(root, "update-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            foreach (var name in new[] { "game.json", "statistics.json", "userSettings.json" })
                File.Copy(Path.Combine(@"C:\Users\Magazzino3\AppData\Local\WordleItaliano", name), Path.Combine(folder, name));
            Environment.SetEnvironmentVariable("WORDLE_STORAGE_FOLDER", folder);
            Environment.SetEnvironmentVariable("WORDLE_TEST_PC_CODE", approvedCode);
            var stats = File.ReadAllBytes(Path.Combine(folder, "statistics.json"));
            using var lease = DataDirectoryLease.Acquire(folder);
            var window = new WordleItaliano.MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false
            };
            try
            {
                var vm = (MainViewModel)window.DataContext;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MainViewModel).GetMethod("CloseOverlays", flags)!.Invoke(vm, null);
                vm.IsProfileDialogVisible = false;
                vm.IsSplashVisible = true;
                var service = typeof(MainViewModel).GetField("_updateService", flags)!.GetValue(vm)!;
                typeof(AppUpdateService).GetField("_manager", flags)!.SetValue(service, new DelayedManager(outcome));
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                window.Show();
                window.UpdateLayout();
                var button = FindPlay(window) ?? throw new InvalidOperationException("Play button missing.");
                var probe = ((AppUpdateService)service).CheckForUpdatesAsync();
                while (!probe.IsCompleted) Pump(100);
                var result = probe.GetAwaiter().GetResult();
                var expected = outcome == "available" ? AppUpdateCheckStatus.Available :
                    outcome == "none" ? AppUpdateCheckStatus.NoUpdates : AppUpdateCheckStatus.Failed;
                if (result.Status != expected)
                    throw new InvalidOperationException("Invalid simulated update response: " + result.Status + " / " + result.ErrorMessage);
                SynchronizationContext.SetSynchronizationContext(null);
                var task = (Task)typeof(MainViewModel).GetMethod("CheckForUpdatesOnStartupAsync", flags)!.Invoke(vm, null)!;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                Pump(early ? 150 : 1600);
                if (task.IsFaulted) task.GetAwaiter().GetResult();
                window.UpdateLayout();
                if (early || outcome != "available")
                {
                    if (!vm.IsSplashVisible || !button.IsEnabled || !button.Command.CanExecute(null))
                        throw new InvalidOperationException("Play unexpectedly unavailable.");
                    var point = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window);
                    var hit = window.InputHitTest(point) as DependencyObject;
                    var chain = new List<string>();
                    for (var visual = hit; visual is not null; visual = VisualTreeHelper.GetParent(visual))
                    {
                        var visibility = visual is FrameworkElement element
                            ? System.Windows.Data.BindingOperations.GetBinding(element, UIElement.VisibilityProperty)?.Path?.Path
                            : null;
                        chain.Add(visual.GetType().Name + ":" + visibility);
                    }
                    while (hit is not null && !ReferenceEquals(hit, button)) hit = VisualTreeHelper.GetParent(hit);
                    if (!ReferenceEquals(hit, button)) throw new InvalidOperationException(
                        $"Play hit blocked ({outcome}, early={early}, visible={button.IsVisible}, point={point}): " + string.Join(" / ", chain));
                    button.Command.Execute(button.CommandParameter);
                    if (vm.IsSplashVisible) throw new InvalidOperationException("Play did not leave splash.");
                }
                else if (vm.IsSplashVisible || !vm.IsUpdateDialogVisible)
                    throw new InvalidOperationException("Available update did not replace splash with visible dialog.");
                Pump(1600);
                if (task.IsFaulted) task.GetAwaiter().GetResult();
                if (outcome == "available")
                {
                    if (!vm.IsUpdateDialogVisible || vm.IsSplashVisible)
                        throw new InvalidOperationException($"Delayed update dialog is missing or hidden behind splash: splash={vm.IsSplashVisible}, dialog={vm.IsUpdateDialogVisible}, task={task.Status}, shown={typeof(MainViewModel).GetField("_updatePromptShownThisSession", flags)!.GetValue(vm)}.");
                    vm.DismissUpdateCommand.Execute(null);
                    if (vm.IsUpdateDialogVisible) throw new InvalidOperationException("Update dialog could not be dismissed.");
                }
                else if (vm.IsUpdateDialogVisible || vm.IsSplashVisible)
                    throw new InvalidOperationException("No-update or failed check blocked navigation.");
                if (task.IsFaulted) task.GetAwaiter().GetResult();
                Console.WriteLine($"PASS: simulated delayed {outcome}, {(early ? "immediate" : "late")} Play; real WPF hit-test and command; no hidden blocking overlay.");
            }
            finally { window.Close(); }
            if (!File.ReadAllBytes(Path.Combine(folder, "statistics.json")).SequenceEqual(stats))
                throw new InvalidOperationException("Startup UI check changed competitive statistics.");
        }
        Console.WriteLine("PASS: six isolated startup/update timing scenarios; no real mouse automation or real save changes.");
    }

    private static Button? FindPlay(DependencyObject root)
    {
        if (root is Button { Content: "Gioca" } button &&
            System.Windows.Data.BindingOperations.GetBinding(button, Button.CommandProperty)?.Path?.Path == "HideSplashCommand")
            return button;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindPlay(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class DelayedManager(string outcome) : UpdateManager("https://example.invalid")
    {
        public override bool IsInstalled => true;
        public override async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            await Task.Delay(1000);
            if (outcome == "failure") throw new IOException("Simulated update connection failure.");
            if (outcome == "none") return null!;
            var asset = new VelopackAsset
            {
                PackageId = "WordleItalianoApp", Version = new SemanticVersion(new Version(1, 5, 27, 0)),
                Type = VelopackAssetType.Full, FileName = "WordleItalianoApp-1.5.27-full.nupkg"
            };
            return new UpdateInfo(asset, false, asset, []);
        }
    }
}
