namespace Sincript.UI;

/// <summary>
/// Console cosmetics, batch parity (D14): magenta-on-black theme ("color 0D"), best-effort
/// 100x36 sizing ("mode con: ... >nul 2>&1"), the SIN logo, and the status-line prefixes.
/// The batch had no per-line colors — everything was theme magenta — so Ok/Warn/Error/Advisory
/// are plain prefixed writes, kept as helpers only so the wording stays in one place.
/// </summary>
internal static class ConsoleUi
{
    public static void Init()
    {
        try { Console.Title = "Sincript - Windows 10/11 Optimizer"; } catch { }
        try
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Magenta;
        }
        catch { }
        TryResize(width: 100, height: 36);
        Cls();
    }

    private static void TryResize(int width, int height)
    {
        // Windows Terminal ignores sizing, legacy conhost honors it, redirected output throws —
        // exactly the situations "mode con: ... >nul 2>&1" swallowed.
        try
        {
            if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
            {
                Console.SetBufferSize(Math.Max(width, Console.BufferWidth), Math.Max(height, Console.BufferHeight));
                Console.SetWindowSize(width, height);
            }
        }
        catch { }
    }

    public static void Cls()
    {
        try { Console.Clear(); } catch { /* redirected output */ }
    }

    /// <summary>:Logo, byte-for-byte (PerfTweaks.cmd 1371-1379): blank, five rows, blank.</summary>
    public static void Logo()
    {
        Blank();
        Line("                          SSSS   III   N   N");
        Line("                          S       I    NN  N");
        Line("                          SSSS    I    N N N");
        Line("                              S   I    N  NN");
        Line("                          SSSS   III   N   N");
        Blank();
    }

    public static void Line(string text) => Console.WriteLine(text);

    /// <summary>Writes a multi-line block exactly as authored (used for the verbatim banners).</summary>
    public static void Block(string text) => Console.WriteLine(text);

    public static void Blank() => Console.WriteLine();

    public static void Ok(string text) => Console.WriteLine($"[OK] {text}");

    public static void Warn(string text) => Console.WriteLine($"[WARN] {text}");

    public static void Error(string text) => Console.WriteLine($"[ERROR] {text}");

    /// <summary>D16 advisory line — informational only, rendered like the other status prefixes.</summary>
    public static void Advisory(string text) => Console.WriteLine($"  [ADVISORY] {text}");

    /// <summary>The batch `pause`: same message, any key, no echo.</summary>
    public static void Pause()
    {
        Console.Write("Press any key to continue . . . ");
        try
        {
            if (!Console.IsInputRedirected)
            {
                Console.ReadKey(intercept: true);
            }
            // Redirected stdin (headless smoke run): don't block, fall straight through —
            // the batch under "< NUL" behaved the same way.
        }
        catch { }
        Console.WriteLine();
    }
}
