using WordleItaliano.PcIdentityCollector;

namespace WordleItaliano.Services;

public sealed record PcAccess(bool IsAuthorized, bool IdentityUnavailable)
{
    public string Message => IdentityUnavailable
        ? "Identita del PC non leggibile. Le sfide ufficiali non sono disponibili; puoi allenarti."
        : IsAuthorized ? string.Empty
        : "Questo PC non e autorizzato alla competizione. Puoi giocare in Allenamento.";
}

public static class PcAuthorization
{
    private static readonly HashSet<string> Approved = new(StringComparer.Ordinal)
    {
        "WIPC1-0E6555960E939D745659175D8E1F1C32C9966E1CEA6B77870DE1425031D2D058",
        "WIPC1-7617CB7E2B9309A3BEB376E7314C221E6FF1C8DD1D83E416EB6F405EAD9488AF",
        "WIPC1-9DD172AA9457F1266FD759674093A327350FE5D699691A21DC6E0AAA0E067C2D"
    };

    public static PcAccess CheckCode(string code) => new(Approved.Contains(code), false);
    public static PcAccess Current => ReadCurrent();

    private static PcAccess ReadCurrent()
    {
#if WORDLE_TEST_BUILD
        var simulated = Environment.GetEnvironmentVariable("WORDLE_TEST_PC_CODE");
        if (simulated == "unreadable") return new(false, true);
        if (simulated is not null) return CheckCode(simulated);
        return new(true, false);
#else
        try { return CheckCode(PcIdentity.ReadCode()); }
        catch (InvalidOperationException) { return new(false, true); }
#endif
    }

    public static void RequireOfficial()
    {
        if (!Current.IsAuthorized) throw new InvalidOperationException("Sfide ufficiali non autorizzate su questo PC.");
    }
}
