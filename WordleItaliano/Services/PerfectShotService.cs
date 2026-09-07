namespace WordleItaliano.Services;

public static class PerfectShotService
{
    public static bool IsPerfectShot(bool won, int attemptCount, bool isInfinite)
    {
        return won && attemptCount == 1 && !isInfinite;
    }
}
