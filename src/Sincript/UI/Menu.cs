namespace Sincript.UI;

/// <summary>
/// The batch menu shape, generalized. Semantics preserved exactly:
///   - empty input re-asks without re-rendering  (goto :X_ask — lives inside MenuChoice);
///   - unknown input re-renders the menu         (goto :X);
///   - a completed action returns here and the menu re-renders (actions ended `goto :X`);
///   - "0" returns to the caller (Back / Exit).
/// </summary>
internal static class Menu
{
    public static void Run(Action renderBanner, IReadOnlyDictionary<string, Action> items)
    {
        while (true)
        {
            renderBanner();
            string sel = Prompts.MenuChoice();
            if (sel == "0") return;
            if (items.TryGetValue(sel, out Action? action)) action();
            // unknown selection: loop → re-render, batch parity
        }
    }
}
