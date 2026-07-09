using Sincript.Core;

namespace Sincript.UI.Screens;

/// <summary>
/// :Excluded (PerfTweaks.cmd 1332-1367) — pure content, implemented fully in P0 (I14: the
/// exclusions and their rationale are part of the product, shipped verbatim).
/// </summary>
internal static class ExcludedScreen
{
    private const string Body =
@"=========================  What was left out (and why)  ===========================
 This script intentionally does NOT include, by category:

 Security-weakening (excluded):
   - Disabling Windows Defender, Firewall, UAC or SmartScreen
   - Removing the ""downloaded from the Internet"" warning on executables
   - Fully disabling Windows Update or pointing it at a fake update server
   - Disabling VBS / HVCI via buggy boot edits
   - Boot flags that turn off DEP, anti-malware early launch, or the hypervisor
     (those also break WSL2 / Hyper-V / Sandbox)

 Placebo / obsolete / harmful (excluded):
   - XP-era ""memory optimization"" registry values (fixed pool/cache sizes etc.)
   - Forcing the large system file cache on by default (it is opt-in under Performance)
   - Clearing the pagefile at shutdown (only makes shutdown slower)
   - Clearing the Prefetch folder (Windows rebuilds it; first launches just get slower)
   - Firewall rules that block Google/YouTube IP ranges to ""stop throttling"" (a myth)
   - Deprecated TCP options (Chimney/NetDMA) removed by Microsoft years ago
   - Hardcoded MTU and other link-specific values copied from another PC
   - Uninstalling old Windows 7/8.1 ""telemetry"" updates (irrelevant on 10/11)
   - Bulk undocumented GPU registry dumps (only vendor telemetry-off is kept, in Advanced)

 From the gaming optimization guide (left out on purpose):
   - Windows activation scripts (MAS) - licensing/trust, not a performance tweak
   - Replacing Defender with a third-party AV (e.g. Panda) - no FPS gain, changes security
   - Aggressive RAM / standby ""cleaners"" (ISLC empty-standby-list) - placebo to harmful
   - Forcing MSI mode, and NIC edits (jumbo frames, offloads) - the guide advises against these

 Note: disabling CPU mitigations and the large system cache ARE available, but only as
 explicit opt-in choices (Advanced / Performance) - never in the recommended set.
=====================================================================================";

    public static void Show(AppSession s)
    {
        ConsoleUi.Cls();
        ConsoleUi.Logo();
        ConsoleUi.Block(Body);
        ConsoleUi.Pause();
    }
}
