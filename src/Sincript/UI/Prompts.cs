namespace Sincript.UI;

/// <summary>
/// Invariant I9: the batch's prompt defaults are a documented safety feature, re-implemented
/// deliberately. Exactly three primitives exist and no screen may roll its own:
///
///   MenuChoice        — empty input re-asks without re-rendering (":X_ask" loops);
///   ConfirmDefaultNo  — the cleared-variable Y/N gates: Enter/anything-but-Y means NO,
///                       so a stray Enter can never fire a destructive step;
///   ConfirmDefaultYes — the restore-point offers (batch pre-set _rp=Y): Enter means YES.
///
/// Matching is the batch's `if /i "%v%"=="Y"`: exact, case-insensitive, no trimming —
/// " y" was not a yes under cmd and is not one here.
/// </summary>
internal static class Prompts
{
    public static string MenuChoice(string prompt = "Choose: ")
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            // EOF / redirected stdin: cmd's `set /p` would spin forever here; returning "0"
            // (back/exit) instead lets a headless smoke run walk out cleanly. Interactive
            // behavior is unaffected — ReadLine never returns null from a live console.
            if (input is null) return "0";

            if (input.Length != 0) return input;
        }
    }

    public static bool ConfirmDefaultNo(string question)
    {
        Console.Write($"{question} (Y/N): ");
        string? input = Console.ReadLine();
        return input is not null && input.Equals("Y", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ConfirmDefaultYes(string question)
    {
        Console.Write($"{question} (Y/N): ");
        string? input = Console.ReadLine();
        // set /p leaves the pre-set "Y" untouched on a bare Enter — and on EOF.
        return input is null
            || input.Length == 0
            || input.Equals("Y", StringComparison.OrdinalIgnoreCase);
    }
}
