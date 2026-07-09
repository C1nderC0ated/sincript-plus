using Sincript.Core;

namespace Sincript.UI.Screens;

internal static class MainMenuScreen
{
    // :MainMenu banner (PerfTweaks.cmd 75-91), verbatim after ^-unescaping. The Build line is
    // rendered separately because it interpolates, and carries the approved D16 machine suffix.
    private const string BannerTop =
@"================================  MAIN MENU  ======================================";
    private const string BannerBody =
@"-----------------------------------------------------------------------------------
    1.  Cleanup & repair        (temp/logs, DISM/SFC, Windows Update, Store, WinSxS)
    2.  Performance tweaks       (GameDVR off, priorities, snappier UI)
    3.  Privacy & telemetry      (telemetry, ads, Cortana, location off)
    4.  Power plan               (high-performance, no sleep)
    5.  Network & DNS            (TCP tweaks, DNS, reset stack)
    6.  Apps & files            (OpenAsar, boot.config, hosts, SteamLight, startup)
    7.  Advanced                 (at your own risk - mitigations, timers, IPv6, GPU)
    8.  Backups & status        (restore point, registry backup, current status)
-----------------------------------------------------------------------------------
    9.  Apply recommended safe set  (one click: 1-5 core tweaks, no prompts)
   10.  Presets (light / moderate / heavy / custom)  + restore preset backup
   11.  What was excluded (info)
    0.  Exit
=====================================================================================";

    public static void Run(AppSession s)
    {
        Menu.Run(
            renderBanner: () =>
            {
                ConsoleUi.Cls();
                ConsoleUi.Logo();
                ConsoleUi.Block(BannerTop);
                ConsoleUi.Line(
                    $"  Build {s.System.WinBuild}   Win11={(s.System.IsWin11 ? 1 : 0)}   GPU={s.System.GpuName}" +
                    $"   Machine={s.System.Hardware.MachineClassName}");
                ConsoleUi.Block(BannerBody);
            },
            items: new Dictionary<string, Action>
            {
                ["1"] = () => SubMenuScreens.Cleanup(s),
                ["2"] = Stubs.NotYet(s, "Performance tweaks", "P2"),
                ["3"] = Stubs.NotYet(s, "Privacy & telemetry", "P2"),
                ["4"] = Stubs.NotYet(s, "Power plan", "P2"),
                ["5"] = () => SubMenuScreens.Network(s),
                ["6"] = () => SubMenuScreens.Apps(s),
                ["7"] = () => SubMenuScreens.Advanced(s),
                ["8"] = () => SubMenuScreens.Backups(s),
                ["9"] = Stubs.NotYet(s, "Apply recommended safe set", "P2"),
                ["10"] = () => SubMenuScreens.Presets(s),
                ["11"] = () => ExcludedScreen.Show(s),
            });

        ExitScreen(s);
    }

    /// <summary>:ExitScript (111-119): logo, log + backup paths, Bye, two-second linger.</summary>
    private static void ExitScreen(AppSession s)
    {
        ConsoleUi.Cls();
        ConsoleUi.Logo();
        ConsoleUi.Line($"  Log saved to: {s.Logger.LogFilePath}");
        ConsoleUi.Line($"  Backups in:   {s.BackupDir}");
        ConsoleUi.Blank();
        ConsoleUi.Line("  Bye.");
        Thread.Sleep(2000);
    }
}
