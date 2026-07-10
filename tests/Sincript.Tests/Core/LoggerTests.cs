using System.Text;
using System.Text.RegularExpressions;
using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// Pins the four contracts <see cref="Logger"/>'s own doc comment claims: the batch's
/// "[stamp] message" line grammar, ">>"-style durable append, D6's BOM-less UTF-8, and the
/// "never let logging take the tool down" failure latch.
/// </summary>
public sealed class LoggerTests : IDisposable
{
    private readonly string _dir;

    public LoggerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sincript-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string PathIn(string name) => Path.Combine(_dir, name);

    [Fact]
    public void Log_writes_the_batch_line_grammar()
    {
        var log = new Logger(PathIn("a.log"));

        log.Log("EXEC: dism /online");

        string line = Assert.Single(File.ReadAllLines(log.LogFilePath));
        Assert.Matches(new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] EXEC: dism /online$"), line);
    }

    [Fact]
    public void Log_appends_rather_than_truncating()
    {
        var log = new Logger(PathIn("b.log"));

        log.Log("first");
        log.Log("second");

        string[] lines = File.ReadAllLines(log.LogFilePath);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("first", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("second", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Log_terminates_each_line_with_the_platform_newline()
    {
        var log = new Logger(PathIn("c.log"));

        log.Log("only");

        Assert.EndsWith(Environment.NewLine, File.ReadAllText(log.LogFilePath), StringComparison.Ordinal);
    }

    /// <summary>D6: real Unicode in the log, and no BOM — a batch-era `type` of the file, and
    /// the batch's own `>>` appends, would both choke on a UTF-8 preamble.</summary>
    [Fact]
    public void Log_writes_utf8_without_a_bom_and_preserves_non_ascii()
    {
        var log = new Logger(PathIn("d.log"));

        log.Log("REGADD Диспетчер задач");

        byte[] raw = File.ReadAllBytes(log.LogFilePath);
        Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
            "log must not start with a UTF-8 BOM");
        Assert.Contains("Диспетчер задач", new UTF8Encoding(false).GetString(raw), StringComparison.Ordinal);
    }

    [Fact]
    public void Log_swallows_io_failure_instead_of_throwing()
    {
        // Parent directory does not exist -> File.AppendAllText throws DirectoryNotFoundException.
        var log = new Logger(Path.Combine(_dir, "missing-subdir", "e.log"));

        log.Log("this must not propagate");

        Assert.False(File.Exists(log.LogFilePath));
    }

    /// <summary>
    /// The latch, not just the swallow: "the FIRST hard IO failure turns the logger into a
    /// no-op". A logger that recovered once its path became writable would emit a log whose
    /// first lines are silently missing — worse than one that stays honestly dead.
    /// </summary>
    [Fact]
    public void Log_stays_dead_after_the_first_failure_even_once_the_path_becomes_writable()
    {
        string subdir = Path.Combine(_dir, "later");
        var log = new Logger(Path.Combine(subdir, "f.log"));

        log.Log("lost");          // fails: subdir absent -> latch trips
        Directory.CreateDirectory(subdir);
        log.Log("also lost");     // path is writable now, but the logger is dead

        Assert.False(File.Exists(log.LogFilePath));
    }
}
