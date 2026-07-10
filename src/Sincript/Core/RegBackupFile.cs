using System.Globalization;

namespace Sincript.Core;

/// <summary>
/// Names the per-value <c>.reg</c> backups (batch :SafeRegAdd 1809-1816).
///
/// D12 replaces the uniq token, not the shape: <c>&lt;sanitized key&gt;_&lt;token&gt;.reg</c> stays,
/// and <c>ManageBackups</c> keeps counting <c>*.reg</c> across both eras (I6). The batch used
/// <c>%RANDOM%%RANDOM%</c> — 30 bits — precisely because two values under one key share a
/// sanitized key name, and a single 15-bit <c>%RANDOM%</c> could birthday-collide inside one apply
/// pass, letting one value's backup overwrite another's and silently destroying that value's undo.
/// A sortable timestamp plus a 122-bit GUID slice keeps that property and sorts chronologically,
/// exactly as D12 already did for the log file name.
/// </summary>
internal static class RegBackupFile
{
    public const string Extension = ".reg";

    /// <summary>
    /// F10: the batch imposed no length bound, so a deep key produced a filename Windows refused
    /// to create — and the failure was swallowed with the rest of the `>nul 2>&1`. Truncating the
    /// head-preserving prefix keeps the hive readable, and the uniq token still guarantees
    /// distinctness, so two keys sharing a 200-character prefix cannot collide.
    /// </summary>
    internal const int MaxKeySegmentLength = 200;

    /// <summary>Batch parity: backslash to underscore, colon dropped, space to underscore.</summary>
    public static string SanitizeKey(string keyPath)
    {
        string safe = keyPath.Replace('\\', '_').Replace(":", "").Replace(' ', '_');
        return safe.Length <= MaxKeySegmentLength ? safe : safe[..MaxKeySegmentLength];
    }

    /// <summary>D12: the 8-hex GUID slice that replaced <c>%RANDOM%%RANDOM%</c>.</summary>
    public static string NewUniq() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Deterministic overload — the clock and the GUID are the caller's to supply, which
    /// is what makes the naming testable at all.</summary>
    public static string NameFor(string keyPath, DateTime timestamp, string uniq)
        => SanitizeKey(keyPath)
         + "_" + timestamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
         + "_" + uniq
         + Extension;

    public static string NameFor(string keyPath) => NameFor(keyPath, DateTime.Now, NewUniq());

    public static string PathFor(string backupDir, string keyPath) => Path.Combine(backupDir, NameFor(keyPath));
}
