using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// <see cref="SystemInfo.Detect"/> reads the live registry, so its assertions must hold on any
/// machine rather than on the author's. What is actually contractual is the *relationship*
/// between the fields (build number -> IsWin11) and the closed vendor set, not the values.
/// </summary>
public sealed class SystemInfoTests
{
    [WindowsOnlyFact]
    public void Detect_derives_IsWin11_from_the_build_number_at_the_documented_threshold()
    {
        var info = SystemInfo.Detect();

        // Batch parity (PerfTweaks.cmd 60-68): Win11 is exactly "CurrentBuildNumber >= 22000".
        // A build that fails to parse (failed reg query) must report IsWin11 = false.
        bool parsed = int.TryParse(info.WinBuild, out int build);
        Assert.Equal(parsed && build >= 22000, info.IsWin11);
    }

    [WindowsOnlyFact]
    public void Detect_never_throws_and_reports_a_vendor_from_the_closed_set()
    {
        var info = SystemInfo.Detect();

        Assert.Contains(info.GpuName, new[] { "nvidia", "amd", "unknown" });
        Assert.NotNull(info.Hardware);
    }
}

/// <summary>
/// The display mapping is a pure function over the enum, so it needs no Windows kernel — the
/// record is constructible directly. This is the split P1 leans on: pure logic tested
/// everywhere, the Win32 seam tested in CI.
/// </summary>
public sealed class HardwareProfileTests
{
    // MachineClass is internal, so it cannot appear in a public test signature (CS0051) —
    // hence discrete facts rather than an [InlineData] theory over the enum.

    [Fact]
    public void Laptop_renders_as_laptop()
        => Assert.Equal("laptop", new HardwareProfile(MachineClass.Laptop).MachineClassName);

    [Fact]
    public void Desktop_renders_as_desktop()
        => Assert.Equal("desktop", new HardwareProfile(MachineClass.Desktop).MachineClassName);

    [Fact]
    public void Unknown_renders_as_unknown()
        => Assert.Equal("unknown", new HardwareProfile(MachineClass.Unknown).MachineClassName);

    [Fact]
    public void Unknown_singleton_reports_the_unknown_class()
        => Assert.Equal("unknown", HardwareProfile.Unknown.MachineClassName);
}
