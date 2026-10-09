using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WordleItaliano.Services;

public sealed class DataDirectoryLease : IDisposable
{
    private static readonly Dictionary<string, DataDirectoryLease> Leases = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileStream _handle;
    private readonly string _folder;
    internal object SyncRoot { get; } = new();

    private DataDirectoryLease(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
        try { _handle = new FileStream(Path.Combine(folder, ".wordle.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33) { throw new DataDirectoryInUseException(); }
        try
        {
            _handle.SetLength(0);
            _handle.Write(BitConverter.GetBytes(Environment.ProcessId));
            _handle.Flush(true);
        }
        catch { _handle.Dispose(); throw; }
    }

    public static string OfficialDataFolder
    {
        get
        {
#if WORDLE_TEST_BUILD
            var isolated = Environment.GetEnvironmentVariable("WORDLE_STORAGE_FOLDER");
            if (isolated is not null) return Path.GetFullPath(isolated);
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WordleItaliano");
        }
    }
    public static string DataFolder => PcAuthorization.Current.IsAuthorized
        ? OfficialDataFolder : Path.Combine(OfficialDataFolder, "training");

    public static DataDirectoryLease Acquire(string folder)
    {
        folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        lock (Leases)
        {
            if (!Leases.TryGetValue(folder, out var lease)) Leases[folder] = lease = new DataDirectoryLease(folder);
            return lease;
        }
    }

    public void Dispose()
    {
        lock (Leases)
        {
            if (Leases.TryGetValue(_folder, out var lease) && ReferenceEquals(lease, this)) Leases.Remove(_folder);
            _handle.Dispose();
        }
    }

    public static void TryActivateExisting(string folder)
    {
        try
        {
            using var stream = new FileStream(Path.Combine(folder, ".wordle.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            using var process = Process.GetProcessById(BitConverter.ToInt32(bytes));
            var window = process.MainWindowHandle;
            if (window == IntPtr.Zero || process.MainWindowTitle != "Wordle Italiano") return;
            if (IsIconic(window)) ShowWindowAsync(window, 9);
            SetForegroundWindow(window);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Activation is optional; the data lock remains authoritative if the window is not ready.
        }
    }

    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}

public sealed class DataDirectoryInUseException : IOException
{
    public DataDirectoryInUseException() : base("Wordle è già aperto su questi dati. Torna alla finestra già aperta per continuare la partita.") { }
}
