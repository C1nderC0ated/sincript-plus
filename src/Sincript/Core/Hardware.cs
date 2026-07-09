using System.Runtime.InteropServices;
using Sincript.UI;

namespace Sincript.Core;

// ============================================================================================
//  D16 (approved): laptop-aware tweak advisories.
//
//  Scope now: detect the machine class (laptop / desktop / unknown) and give the catalog a
//  way to mark tweaks that are typically counterproductive on one class. Advisories are
//  WARNING-ONLY: they never block an action, never change a prompt default, never alter what
//  a preset applies — the opt-in philosophy (plan §0 rule 1) is information, not gatekeeping.
//
//  Forward compatibility with the deferred full detection engine (plan §11): HardwareProfile
//  is the single record every consumer reads; the engine later replaces HardwareDetector and
//  widens the record (CPU, RAM, GPU details, storage, AC/battery state) without touching any
//  consumer. TweakAdvisory is [Flags] so new conditions (HarmfulOnLowRam, ...) slot in beside
//  the existing ones, and Advisories.WarnIfApplicable stays the one rendering choke point.
// ============================================================================================

internal enum MachineClass { Unknown, Desktop, Laptop }

[Flags]
internal enum TweakAdvisory
{
    None = 0,

    /// <summary>Typically hurts battery life / thermals on portable machines
    /// (power core, hibernate-off, BCD dynamic-tick off, timer-resolution autostart).</summary>
    HarmfulOnLaptop = 1 << 0,

    /// <summary>Mainly helps some laptops and can hurt desktops — the batch's own wording
    /// for LargeSystemCache ("can help some laptops, can hurt desktops").</summary>
    HarmfulOnDesktop = 1 << 1,

    // Reserved for the full engine: HarmfulOnLowRam = 1 << 2, RequiresDesktopGpu = 1 << 3, ...
}

/// <summary>Everything the app knows about the hardware. Deliberately a record: the full
/// detection engine extends it with more fields without breaking a single consumer.</summary>
internal sealed record HardwareProfile(MachineClass MachineClass)
{
    public static readonly HardwareProfile Unknown = new(MachineClass.Unknown);

    /// <summary>Lowercase display form for banners/log ("laptop" / "desktop" / "unknown").</summary>
    public string MachineClassName => MachineClass switch
    {
        MachineClass.Laptop => "laptop",
        MachineClass.Desktop => "desktop",
        _ => "unknown",
    };
}

internal static class Advisories
{
    /// <summary>
    /// Renders the advisory line(s) for an action carrying the given flags, when they apply
    /// to this machine. Called by actions right before their confirm prompt (wired in P2).
    /// Must never throw and never change control flow.
    /// </summary>
    public static void WarnIfApplicable(AppSession session, TweakAdvisory flags)
    {
        MachineClass mc = session.System.Hardware.MachineClass;

        if (flags.HasFlag(TweakAdvisory.HarmfulOnLaptop) && mc == MachineClass.Laptop)
            ConsoleUi.Advisory("this machine looks like a laptop - this action typically costs battery life and heat there for little gain. It stays your call, and stays reversible.");

        if (flags.HasFlag(TweakAdvisory.HarmfulOnDesktop) && mc == MachineClass.Desktop)
            ConsoleUi.Advisory("this machine looks like a desktop - this option mainly helps some laptops and can hurt desktop performance.");
    }
}

/// <summary>
/// Phase-P0 detector: POWER_PLATFORM_ROLE first (the documented API for exactly this
/// question), battery presence as the tie-breaker for Unspecified. Pure P/Invoke, AOT-safe,
/// never throws — an undetectable machine is MachineClass.Unknown and no advisory fires.
/// </summary>
internal static partial class HardwareDetector
{
    public static HardwareProfile Detect()
    {
        try
        {
            return new HardwareProfile(DetectMachineClass());
        }
        catch
        {
            return HardwareProfile.Unknown;
        }
    }

    private static MachineClass DetectMachineClass()
    {
        // POWER_PLATFORM_ROLE, version 2 semantics.
        const uint POWER_PLATFORM_ROLE_V2 = 2;
        int role;
        try { role = PowerDeterminePlatformRoleEx(POWER_PLATFORM_ROLE_V2); }
        catch { role = 0; }

        switch (role)
        {
            case 2: // PlatformRoleMobile
            case 8: // PlatformRoleSlate
                return MachineClass.Laptop;
            case 1: // PlatformRoleDesktop
            case 3: // PlatformRoleWorkstation
            case 4: // PlatformRoleEnterpriseServer
            case 5: // PlatformRoleSOHOServer
            case 6: // PlatformRoleAppliancePC
            case 7: // PlatformRolePerformanceServer
                return MachineClass.Desktop;
        }

        // PlatformRoleUnspecified (or the call failed): corroborate with battery presence.
        try
        {
            if (GetSystemPowerStatus(out SystemPowerStatus status))
            {
                const byte BATTERY_FLAG_NO_SYSTEM_BATTERY = 128;
                const byte BATTERY_FLAG_UNKNOWN = 255;
                if (status.BatteryFlag == BATTERY_FLAG_UNKNOWN) return MachineClass.Unknown;
                return (status.BatteryFlag & BATTERY_FLAG_NO_SYSTEM_BATTERY) != 0
                    ? MachineClass.Desktop
                    : MachineClass.Laptop;
            }
        }
        catch { /* fall through */ }

        return MachineClass.Unknown;
    }

    // Classic DllImport on blittable signatures: fully NativeAOT-compatible, no unsafe needed.
    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern int PowerDeterminePlatformRoleEx(uint version);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
