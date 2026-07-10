namespace Sincript.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself off Windows.
///
/// The tool is Windows-only, but most of its risky logic is not: <c>.reg</c> serialization
/// (D2) and the <c>AlreadyAtTarget</c> equality matrix (D3) are pure functions over value
/// kinds and bytes, and stay testable on any dev host. Only the code that actually crosses
/// into Win32 — the registry read/write seam, <c>Elevation</c>, <c>Hardware</c>'s P/Invokes —
/// needs a real Windows kernel underneath.
///
/// Marking those with <c>[WindowsOnlyFact]</c> keeps `dotnet test` green on a macOS/Linux
/// dev host while CI on windows-latest runs the full set. A skip is reported, not silently
/// passed, so the coverage gap stays visible in the runner output.
/// </summary>
internal sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows-only: crosses the Win32/registry seam. Runs in CI on windows-latest.";
    }
}
