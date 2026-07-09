using Sincript.Core;

namespace Sincript.UI.Screens;

/// <summary>
/// P0 stubs for every sub-menu: banners are verbatim ports of the batch screens (^-unescaped),
/// navigation is fully live, actions announce their implementation phase. Each menu will move
/// into its own file as it gains a real body (plan §3 layout), keeping this file temporary.
/// </summary>
internal static class SubMenuScreens
{
    // ---------------------------------------------------------------- Cleanup & repair (123-145)
    private const string CleanupBanner =
@"============================  CLEANUP & REPAIR  ===================================
    1.  Clean temp / logs / caches      (+ optional: clear all Event Viewer logs)
    2.  DISM + SFC system integrity
    3.  Reset Windows Update components
    4.  Re-register Microsoft Store / apps
    5.  Compact WinSxS (free disk space)
    0.  Back
=====================================================================================";

    public static void Cleanup(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(CleanupBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Clean temp / logs / caches", "P2"),
            ["2"] = Stubs.NotYet(s, "DISM + SFC system integrity", "P2"),
            ["3"] = Stubs.NotYet(s, "Reset Windows Update components", "P2"),
            ["4"] = Stubs.NotYet(s, "Re-register Microsoft Store / apps", "P2"),
            ["5"] = Stubs.NotYet(s, "Compact WinSxS", "P2"),
        });

    // ---------------------------------------------------------------- Network & DNS (149-190)
    private const string NetworkBanner =
@"=============================  NETWORK & DNS  =====================================
    1.  Apply TCP tweaks        (autotuning/heuristics/RSS/RSC, optional low-latency)
    2.  Set DNS                 (Cloudflare / Google / Quad9 / automatic)
    3.  Reset network stack     (winsock / ip / dns)
    0.  Back
=====================================================================================";

    public static void Network(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(NetworkBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Apply TCP tweaks", "P2"),
            ["2"] = () => Dns(s),
            ["3"] = Stubs.NotYet(s, "Reset network stack", "P2"),
        });

    private const string DnsBanner =
@"===============================  SET DNS  =========================================
 IPv4 + IPv6, applied to all active adapters, DNS cache flushed. Fully reversible.
    1.  Cloudflare   1.1.1.1 / 1.0.0.1
    2.  Google       8.8.8.8 / 8.8.4.4
    3.  Quad9        9.9.9.9 / 149.112.112.112   (blocks known-malicious domains)
    4.  Revert to automatic (DHCP)
    0.  Back
=====================================================================================";

    private static void Dns(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(DnsBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Set DNS: Cloudflare", "P2"),
            ["2"] = Stubs.NotYet(s, "Set DNS: Google", "P2"),
            ["3"] = Stubs.NotYet(s, "Set DNS: Quad9", "P2"),
            ["4"] = Stubs.NotYet(s, "Revert DNS to automatic (DHCP)", "P2"),
        });

    // ---------------------------------------------------------------- Apps & files (194-224)
    private const string AppsBanner =
@"=============================  APPS & FILES  ======================================
    1.  Install OpenAsar into Discord
    2.  Place Unity boot.config into a game folder
    3.  Apply custom hosts file (ad/telemetry blocklist)
    4.  Restore / reset hosts
    5.  Install SteamLight (lightweight Steam launcher + desktop shortcut)
    6.  Apply timer resolution (SetTimerResolution autostart)
    7.  Remove timer resolution
    8.  Remove built-in apps (debloat)
    9.  Manage startup programs (enable / disable, reversible)
    0.  Back
=====================================================================================";

    public static void Apps(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(AppsBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Install OpenAsar into Discord", "P4"),
            ["2"] = Stubs.NotYet(s, "Place Unity boot.config into a game folder", "P4"),
            ["3"] = Stubs.NotYet(s, "Apply custom hosts file", "P4"),
            ["4"] = Stubs.NotYet(s, "Restore / reset hosts", "P4"),
            ["5"] = Stubs.NotYet(s, "Install SteamLight", "P4"),
            ["6"] = Stubs.NotYet(s, "Apply timer resolution", "P4"),
            ["7"] = Stubs.NotYet(s, "Remove timer resolution", "P4"),
            ["8"] = Stubs.NotYet(s, "Remove built-in apps (debloat)", "P4"),
            ["9"] = Stubs.NotYet(s, "Manage startup programs", "P4"),
        });

    // ---------------------------------------------------------------- Advanced (228-261)
    private const string AdvancedBannerTop =
@"====================  ADVANCED  -  AT YOUR OWN RISK  ===============================
 Reversible, never part of ""Apply recommended"". Most need a reboot.
    1.  Disable CPU mitigations        (faster, LESS secure)
    2.  Re-enable CPU mitigations      (secure default)
    3.  BCDEdit timer tweaks
    4.  Revert BCDEdit timer tweaks
    5.  Experimental NVMe driver flags
    6.  Disable IPv6 (all adapters)
    7.  Disable memory compression / page combining";
    private const string AdvancedBannerBottom =
@"    9.  GPU hardware scheduling (HAGS) on/off
   10.  Set permanent process priority  (per .exe, e.g. a game)
    0.  Back
=====================================================================================";

    public static void Advanced(AppSession s) => Menu.Run(
        () =>
        {
            ConsoleUi.Cls();
            ConsoleUi.Logo();
            ConsoleUi.Block(AdvancedBannerTop);
            ConsoleUi.Line($"    8.  {s.System.GpuName} telemetry / background tasks off"); // %GPU% interpolation
            ConsoleUi.Block(AdvancedBannerBottom);
        },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Disable CPU mitigations", "P5"),
            ["2"] = Stubs.NotYet(s, "Re-enable CPU mitigations", "P5"),
            ["3"] = Stubs.NotYet(s, "BCDEdit timer tweaks", "P5"),
            ["4"] = Stubs.NotYet(s, "Revert BCDEdit timer tweaks", "P5"),
            ["5"] = Stubs.NotYet(s, "Experimental NVMe driver flags", "P5"),
            ["6"] = Stubs.NotYet(s, "Disable IPv6", "P5"),
            ["7"] = Stubs.NotYet(s, "Disable memory compression / page combining", "P5"),
            ["8"] = Stubs.NotYet(s, "GPU telemetry / background tasks off", "P5"),
            ["9"] = Stubs.NotYet(s, "GPU hardware scheduling (HAGS) on/off", "P5"),
            ["10"] = Stubs.NotYet(s, "Set permanent process priority", "P5"),
        });

    // ---------------------------------------------------------------- Backups & status (265-289)
    private const string BackupsBanner =
@"===========================  BACKUPS & STATUS  ====================================
    1.  Create System Restore Point
    2.  Full registry backup (HKLM + HKCU export)
    3.  Show current status / what's applied
    4.  Restore from a preset backup (JSON)
    5.  Restore a single value backup (.reg)
    6.  Manage / open backup folder
    0.  Back
=====================================================================================";

    public static void Backups(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(BackupsBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Create System Restore Point", "P5"),
            ["2"] = Stubs.NotYet(s, "Full registry backup", "P5"),
            ["3"] = Stubs.NotYet(s, "Show current status", "P5"),
            ["4"] = Stubs.NotYet(s, "Restore from a preset backup (JSON)", "P3"),
            ["5"] = Stubs.NotYet(s, "Restore a single value backup (.reg)", "P5"),
            ["6"] = Stubs.NotYet(s, "Manage / open backup folder", "P5"),
        });

    // ---------------------------------------------------------------- Presets (2016-2042)
    private const string PresetsBanner =
@"==============================  AUTO-APPLY PRESETS  ===============================
 A preset applies a defined group of tweaks at once and saves ONE JSON backup of the
 registry values it changes (manual menu actions still save individual .reg files).
 Power-plan / DNS / BCD / service changes revert from their own menu items.
-----------------------------------------------------------------------------------
    1.  Light     (temp cleanup, privacy, TCP tweaks, DNS)
    2.  Moderate  (recommended safe set + power plan + OpenAsar)
    3.  Heavy     (most tweaks; NO repair / NO stack reset / NO debloat / NO mitigations)
    4.  Custom    (load a user preset from the sincript_presets folder)
    5.  Restore from a preset backup (JSON)
    0.  Back
=====================================================================================";

    public static void Presets(AppSession s) => Menu.Run(
        () => { ConsoleUi.Cls(); ConsoleUi.Logo(); ConsoleUi.Block(PresetsBanner); },
        new Dictionary<string, Action>
        {
            ["1"] = Stubs.NotYet(s, "Preset: Light", "P3"),
            ["2"] = Stubs.NotYet(s, "Preset: Moderate", "P3"),
            ["3"] = Stubs.NotYet(s, "Preset: Heavy", "P3"),
            ["4"] = Stubs.NotYet(s, "Custom preset", "P3"),
            ["5"] = Stubs.NotYet(s, "Restore from a preset backup (JSON)", "P3"),
        });
}
