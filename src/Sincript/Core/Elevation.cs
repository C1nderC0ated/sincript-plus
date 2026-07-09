using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Sincript.UI;

namespace Sincript.Core;

internal enum RelaunchOutcome
{
    /// <summary>The elevated child was started; this process should exit.</summary>
    ChildStarted,
    /// <summary>The user declined the UAC prompt (ERROR_CANCELLED, 1223).</summary>
    Declined,
    /// <summary>The relaunch could not be attempted or failed for another reason.</summary>
    Failed,
}

internal static class Elevation
{
    /// <summary>Replaces the batch three-probe chain (net session || fltmc || reg query HKU).</summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false; // treat unknown as not elevated; actions report honestly either way (I5)
        }
    }

    /// <summary>
    /// Relaunches this executable with the "runas" verb and the /elevated one-shot marker.
    /// ProcessStartInfo never string-interpolates the path, so the whole apostrophe-in-path
    /// bug class the batch fixed via $env:PT_SELF is structurally gone (plan §6.6).
    /// </summary>
    public static RelaunchOutcome TryRelaunchElevated()
    {
        try
        {
            string self = Environment.ProcessPath
                          ?? Path.Combine(AppContext.BaseDirectory, "Sincript.exe");
            var psi = new ProcessStartInfo
            {
                FileName = self,
                Arguments = "/elevated",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(self) ?? AppContext.BaseDirectory,
            };
            using var child = Process.Start(psi);
            return child is not null ? RelaunchOutcome.ChildStarted : RelaunchOutcome.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            return RelaunchOutcome.Declined;
        }
        catch
        {
            return RelaunchOutcome.Failed;
        }
    }

    /// <summary>
    /// The :AdminWarn screen, text verbatim (PerfTweaks.cmd 33-40). Returns true when the
    /// user explicitly opts into limited (per-user only) mode. Empty input means No (I9).
    /// </summary>
    public static bool OfferLimitedMode()
    {
        ConsoleUi.Blank();
        ConsoleUi.Line("[WARN] Not running as Administrator. HKLM / service / boot / hosts changes WILL fail;");
        ConsoleUi.Line("       only per-user (HKCU) tweaks and the read-only status screens can work in this mode.");
        ConsoleUi.Line("       For the full toolset, close this window and use \"Run as administrator\".");
        ConsoleUi.Blank();
        return Prompts.ConfirmDefaultNo("Continue anyway in limited (per-user only) mode?");
    }
}
