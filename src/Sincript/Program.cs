using System.Text;
using Sincript.Core;
using Sincript.UI;
using Sincript.UI.Screens;

namespace Sincript;

internal static class Program
{
    private static int Main(string[] args)
    {
        // D6 (approved): real Unicode on the console and in the log. Cyrillic startup-entry
        // names, non-ASCII paths etc. render instead of being '?'-masked as under cmd.
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected / legacy host */ }

        // Plan §2 bitness contract: fail loudly rather than silently read the WOW64 registry
        // view. PlatformTarget=x64 makes this unreachable in practice; belt and suspenders.
        if (!Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("[ERROR] Sincript must run as a 64-bit process so registry paths");
            Console.Error.WriteLine("        (including WOW6432Node) resolve exactly like the batch original.");
            return 1;
        }

        ConsoleUi.Init();

        // ---------- Self-elevate to Administrator (robust, cannot loop) ----------
        // Batch parity (:AdminWarn/:AdminOK, PerfTweaks.cmd 14-43): the three-probe chain
        // (net session || fltmc || reg query HKU) was a stand-in for exactly this API and
        // dies whole (plan rule 3). The "/elevated" one-shot marker survives as the loop guard.
        bool alreadyRelaunched = args.Any(a => a.Equals("/elevated", StringComparison.OrdinalIgnoreCase));
        bool elevated = Elevation.IsElevated();

        if (!elevated && !alreadyRelaunched)
        {
            Console.WriteLine("Requesting Administrator privileges...");
            switch (Elevation.TryRelaunchElevated())
            {
                case RelaunchOutcome.ChildStarted:
                    return 0; // the elevated child owns the session from here (batch parity)

                case RelaunchOutcome.Declined:
                case RelaunchOutcome.Failed:
                    // D11 (approved): the batch parent exited here, so a declined UAC prompt
                    // just closed the window and limited mode was unreachable on that path.
                    // We fall through to the same warn/opt-in screen the relaunched child
                    // would have shown, making the documented feature reachable.
                    break;
            }
        }

        if (!elevated && !Elevation.OfferLimitedMode())
            return 0;

        var session = AppSession.Create(elevated);

        // Batch start line + the D16 machine-class suffix (approved delta, DECISIONS.md).
        session.Logger.Log(
            $"PerfTweaks start - build {session.System.WinBuild} win11={(session.System.IsWin11 ? 1 : 0)} " +
            $"gpu={session.System.GpuName} machine={session.System.Hardware.MachineClassName}");

        MainMenuScreen.Run(session);
        return 0;
    }
}
