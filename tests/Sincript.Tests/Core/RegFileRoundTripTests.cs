using System.Diagnostics;
using System.Text;
using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// The oracle. Every other RegFileWriter test asserts against our *model* of the `.reg` format;
/// if the model is wrong they are confidently wrong with it. Here Windows is the judge: we
/// serialize a PriorValue, hand the file to `reg import`, read the value back through
/// RegQueryValueEx, and require the bytes to come back identical.
///
/// This is the only test that can prove D2's claim — that every kind backs up *restorably* —
/// and it can only run on Windows, so CI is where it earns its keep.
/// </summary>
public sealed class RegFileRoundTripTests : IDisposable
{
    private readonly string _shortRoot = @"HKCU\Software\Sincript.Tests\" + Guid.NewGuid().ToString("N")[..8];

    private static byte[] Sz(string s) => Encoding.Unicode.GetBytes(s + "\0");

    private static string Hex(byte[] b) => b.Length == 0 ? "<empty>" : Convert.ToHexString(b).ToLowerInvariant();

    private static int RunReg(params string[] args)
    {
        var psi = new ProcessStartInfo("reg.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using Process p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode;
    }

    public void Dispose()
    {
        // Every fact here is [WindowsOnlyFact], so xUnit should never construct this class off
        // Windows — but reg.exe would throw rather than skip if it ever did.
        if (OperatingSystem.IsWindows()) RunReg("delete", _shortRoot, "/f");
    }

    /// <summary>Serialize, import, read back. Returns what the registry actually holds.</summary>
    private PriorValue ImportAndCapture(PriorValue value)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8] + ".reg");
        File.WriteAllBytes(file, RegFileWriter.BuildBytes(value));
        try
        {
            int rc = RunReg("import", file);
            Assert.True(rc == 0, $"reg import failed (exit {rc}) for:\n{RegFileWriter.BuildText(value)}");
            return RegistryService.Capture(_shortRoot, value.ValueName);
        }
        finally
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    private (string Label, string Name, RegKind Kind, byte[] Data)[] Cases() =>
    [
        ("REG_SZ ascii",            "sz",        RegKind.Sz,       Sz("hello")),
        ("REG_SZ escaping",         "esc",       RegKind.Sz,       Sz(@"C:\Program Files\a""b""")),
        ("REG_SZ non-ascii (D2)",   "cyr",       RegKind.Sz,       Sz("Диспетчер задач")),
        ("REG_SZ empty",            "szempty",   RegKind.Sz,       [0, 0]),
        ("REG_SZ default value",    "",          RegKind.Sz,       Sz("default")),
        ("REG_EXPAND_SZ unexpanded", "exp",      RegKind.ExpandSz, Sz("%SystemRoot%")),
        ("REG_DWORD",               "dw",        RegKind.Dword,    [0xEF, 0xBE, 0xAD, 0xDE]),
        ("REG_DWORD max",           "dwmax",     RegKind.Dword,    [0xFF, 0xFF, 0xFF, 0xFF]),
        ("REG_QWORD",               "qw",        RegKind.Qword,    [1, 2, 3, 4, 5, 6, 7, 8]),
        ("REG_BINARY",              "bin",       RegKind.Binary,   [0xDE, 0xAD, 0xBE, 0xEF]),
        ("REG_BINARY empty",        "binempty",  RegKind.Binary,   []),
        ("REG_BINARY wrapping",     "binlong",   RegKind.Binary,   Enumerable.Range(0, 200).Select(i => (byte)i).ToArray()),
        ("REG_MULTI_SZ",            "multi",     RegKind.MultiSz,  [0x61, 0, 0, 0, 0x62, 0, 0, 0, 0, 0]),
        ("REG_MULTI_SZ empty",      "multiempty", RegKind.MultiSz, [0, 0]),
        ("REG_NONE",                "none",      RegKind.None,     []),
    ];

    [WindowsOnlyFact]
    public void Reg_import_reproduces_the_exact_bytes_for_every_kind()
    {
        string fullRoot = RegistryService.ToFullHivePath(_shortRoot);
        var failures = new List<string>();

        foreach ((string label, string name, RegKind kind, byte[] data) in Cases())
        {
            var expected = new PriorValue(fullRoot, name, Present: true, kind, data);
            PriorValue actual = ImportAndCapture(expected);

            if (!actual.Present) { failures.Add($"{label}: value absent after import"); continue; }
            if (actual.Kind != kind) { failures.Add($"{label}: kind {actual.Kind} != {kind}"); continue; }
            if (!actual.Data.SequenceEqual(data))
                failures.Add($"{label}: bytes differ\n    expected {Hex(data)}\n    actual   {Hex(actual.Data)}");
        }

        // Report every mismatch at once: a per-kind bug is far easier to read as a list than as
        // fifteen sequential red runs.
        Assert.True(failures.Count == 0, "reg import round trip failed:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>Batch parity (:BackupValueLine 1884-1887): an absent value backs up as "name"=-,
    /// and importing that deletes whatever is there now.</summary>
    [WindowsOnlyFact]
    public void An_absent_value_backs_up_as_a_deletion_that_reg_import_honors()
    {
        RunReg("add", _shortRoot, "/v", "doomed", "/t", "REG_SZ", "/d", "still here", "/f");
        Assert.True(RegistryService.Capture(_shortRoot, "doomed").Present, "precondition: value should exist");

        PriorValue restored = ImportAndCapture(PriorValue.Absent(RegistryService.ToFullHivePath(_shortRoot), "doomed"));

        Assert.False(restored.Present, "\"doomed\"=- should have removed the value");
    }

    /// <summary>The trap F7 exists to name: a REG_EXPAND_SZ must survive as %SystemRoot%, not as
    /// the expansion of it on whatever machine took the backup.</summary>
    [WindowsOnlyFact]
    public void Expand_sz_round_trips_the_literal_percent_variable()
    {
        var expected = new PriorValue(RegistryService.ToFullHivePath(_shortRoot), "exp2", true, RegKind.ExpandSz, Sz("%SystemRoot%\\Temp"));

        PriorValue actual = ImportAndCapture(expected);

        Assert.Equal(RegKind.ExpandSz, actual.Kind);
        Assert.Equal("%SystemRoot%\\Temp", Encoding.Unicode.GetString(actual.Data)[..^1]);
    }
}
