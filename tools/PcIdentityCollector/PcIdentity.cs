using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace WordleItaliano.PcIdentityCollector;

public static class PcIdentity
{
    public const string HashPrefix = "WordleItaliano|PC-Installation|v1|";

    public static string ReadCode() => ReadCode(ReadMachineGuid);

    public static string ReadCode(Func<string?> readIdentifier)
    {
        try
        {
            return FromIdentifier(readIdentifier());
        }
        catch (Exception error) when (error is UnauthorizedAccessException
            or System.Security.SecurityException or System.IO.IOException
            or ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Non riesco a leggere il codice di questa installazione Windows. " +
                "Nessun codice sostitutivo e' stato generato. Chiedi assistenza.", error);
        }
    }

    public static string FromIdentifier(string? identifier)
    {
        if (!Guid.TryParse(identifier?.Trim(), out var guid) || guid == Guid.Empty)
            throw new ArgumentException("Identificativo Windows assente o non valido.");
        var normalized = guid.ToString("D").ToLowerInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(HashPrefix + normalized));
        return "WIPC1-" + Convert.ToHexString(digest);
    }

    private static string? ReadMachineGuid()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
        return key?.GetValue("MachineGuid", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }
}
