using System.Windows;
using Velopack;
using WordleItaliano.Services;

namespace WordleItaliano;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        try
        {
            using var lease = DataDirectoryLease.Acquire(DataDirectoryLease.DataFolder);
            var app = new App();
            app.InitializeComponent();
            app.DispatcherUnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.Exception is not StorageFailureException) return;
                eventArgs.Handled = true;
                MessageBox.Show(eventArgs.Exception.Message, "Dati da recuperare", MessageBoxButton.OK, MessageBoxImage.Warning);
                app.Shutdown(1);
            };
            app.Run(new MainWindow());
        }
        catch (Exception error) when (error is DataDirectoryInUseException or StorageFailureException or System.IO.IOException or UnauthorizedAccessException)
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WordleItaliano-startup-error.log"),
                    $"{error.GetType().FullName}\nHResult: {error.HResult}\n{error.StackTrace}");
            }
            catch (Exception logError) when (logError is System.IO.IOException or UnauthorizedAccessException) { }
            if (error is DataDirectoryInUseException) DataDirectoryLease.TryActivateExisting(DataDirectoryLease.DataFolder);
            MessageBox.Show(error is DataDirectoryInUseException or StorageFailureException ? error.Message :
                "Non è possibile accedere alla cartella dati. I dati non sono stati cancellati. Chiedi assistenza.",
                "Wordle Italiano", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
