using Microsoft.Win32;

namespace Sincript.Core;

internal enum GpuVendor { Unknown, Nvidia, Amd }

/// <summary>
/// The startup detections from PerfTweaks.cmd 60-68: OS build, Win11 (build >= 22000), and
/// GPU vendor via a DriverDesc scan of the display class key — same source, typed access.
/// Extended per D16 with the machine-class hardware profile.
/// </summary>
internal sealed class SystemInfo
{
    public string WinBuild { get; }
    public bool IsWin11 { get; }
    public GpuVendor Gpu { get; }
    public HardwareProfile Hardware { get; }

    /// <summary>Lowercase vendor name exactly as the batch %GPU% displayed it.</summary>
    public string GpuName => Gpu switch
    {
        GpuVendor.Nvidia => "nvidia",
        GpuVendor.Amd => "amd",
        _ => "unknown",
    };

    private SystemInfo(string winBuild, bool isWin11, GpuVendor gpu, HardwareProfile hardware)
    {
        WinBuild = winBuild;
        IsWin11 = isWin11;
        Gpu = gpu;
        Hardware = hardware;
    }

    public static SystemInfo Detect()
    {
        string build = "";
        try
        {
            build = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "CurrentBuildNumber", null) as string ?? "";
        }
        catch { /* WIN_BUILD stays empty, same as a failed reg query */ }

        bool isWin11 = int.TryParse(build, out int b) && b >= 22000;

        return new SystemInfo(build, isWin11, DetectGpu(), HardwareDetector.Detect());
    }

    /// <summary>
    /// Batch parity (lines 66-67): two findstr passes over the display class key; the AMD
    /// check ran second and overwrote, so AMD wins when both vendors are present. Preserved.
    /// </summary>
    private static GpuVendor DetectGpu()
    {
        bool hasNvidia = false, hasAmd = false;
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey is null) return GpuVendor.Unknown;

            foreach (string sub in classKey.GetSubKeyNames())
            {
                try
                {
                    using var device = classKey.OpenSubKey(sub);
                    if (device?.GetValue("DriverDesc") is not string desc) continue;
                    if (desc.Contains("nvidia", StringComparison.OrdinalIgnoreCase)) hasNvidia = true;
                    if (desc.Contains("radeon", StringComparison.OrdinalIgnoreCase)) hasAmd = true;
                }
                catch { /* skip unreadable device subkeys, as reg query /s did implicitly */ }
            }
        }
        catch { return GpuVendor.Unknown; }

        return hasAmd ? GpuVendor.Amd
             : hasNvidia ? GpuVendor.Nvidia
             : GpuVendor.Unknown;
    }
}
