using Sincript.Core;

namespace Sincript.UI.Screens;

internal static class Stubs
{
    /// <summary>
    /// P0 placeholder for a not-yet-ported action. States the phase honestly (the tool's own
    /// reporting philosophy applies to its scaffolding too), logs the visit, and returns to
    /// the owning menu like every batch action did.
    /// </summary>
    public static Action NotYet(AppSession s, string name, string phase) => () =>
    {
        ConsoleUi.Blank();
        ConsoleUi.Line($"  [TODO] \"{name}\" is not ported yet - scheduled for Phase {phase} of the C# migration.");
        ConsoleUi.Line("         Until then, the batch PerfTweaks.cmd remains the working implementation.");
        s.Logger.Log($"STUB visited: {name} ({phase})");
        ConsoleUi.Pause();
    };
}
