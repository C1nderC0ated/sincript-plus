using System.Text.RegularExpressions;
using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>D12: the uniq token changes, the shape does not — ManageBackups still counts *.reg
/// across both eras, and the name still cannot collide within one apply pass.</summary>
public sealed class RegBackupFileTests
{
    private static readonly DateTime Stamp = new(2026, 7, 10, 1, 2, 3, DateTimeKind.Local);

    // ---------- batch-parity sanitization (PerfTweaks.cmd 1810-1812) ----------

    [Fact]
    public void Backslashes_become_underscores()
        => Assert.Equal("HKLM_Software_X", RegBackupFile.SanitizeKey(@"HKLM\Software\X"));

    [Fact]
    public void Spaces_become_underscores()
        => Assert.Equal("HKLM_Program_Files", RegBackupFile.SanitizeKey(@"HKLM\Program Files"));

    [Fact]
    public void Colons_are_dropped_entirely_not_replaced()
        => Assert.Equal("HKLM_Cdrive", RegBackupFile.SanitizeKey(@"HKLM\C:drive"));

    // ---------- the name ----------

    [Fact]
    public void Name_is_sanitized_key_then_sortable_stamp_then_uniq_then_reg()
        => Assert.Equal("HKLM_Software_X_20260710_010203_deadbeef",
                        Path.GetFileNameWithoutExtension(RegBackupFile.NameFor(@"HKLM\Software\X", Stamp, "deadbeef")));

    [Fact]
    public void Name_ends_in_the_reg_extension_ManageBackups_counts()
        => Assert.EndsWith(".reg", RegBackupFile.NameFor(@"HKLM\Software\X", Stamp, "deadbeef"), StringComparison.Ordinal);

    [Fact]
    public void Generated_names_match_the_documented_shape()
        => Assert.Matches(new Regex(@"^HKCU_Software_X_\d{8}_\d{6}_[0-9a-f]{8}\.reg$"),
                          RegBackupFile.NameFor(@"HKCU\Software\X"));

    /// <summary>The batch's stated reason for a 30-bit token: two values under one key share a
    /// sanitized name, so the uniq must not repeat within an apply pass.</summary>
    [Fact]
    public void Two_backups_of_the_same_key_get_different_names()
        => Assert.NotEqual(RegBackupFile.NameFor(@"HKLM\Software\X"), RegBackupFile.NameFor(@"HKLM\Software\X"));

    [Fact]
    public void Uniq_tokens_are_eight_lowercase_hex_digits()
        => Assert.Matches(new Regex("^[0-9a-f]{8}$"), RegBackupFile.NewUniq());

    /// <summary>D12's other promise: names sort chronologically, which %RANDOM% never did.</summary>
    [Fact]
    public void Names_sort_chronologically_for_the_same_key()
    {
        string earlier = RegBackupFile.NameFor(@"HKLM\X", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local), "aaaaaaaa");
        string later = RegBackupFile.NameFor(@"HKLM\X", new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Local), "00000000");

        Assert.True(string.CompareOrdinal(earlier, later) < 0, $"{earlier} should sort before {later}");
    }

    // ---------- F10: length bound ----------

    [Fact]
    public void An_overlong_key_is_truncated_head_first_so_the_hive_stays_readable()
    {
        string deep = @"HKLM\" + string.Join(@"\", Enumerable.Repeat("segment", 60));

        string sanitized = RegBackupFile.SanitizeKey(deep);

        Assert.Equal(RegBackupFile.MaxKeySegmentLength, sanitized.Length);
        Assert.StartsWith("HKLM_segment", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void A_truncated_key_still_produces_a_filename_windows_will_accept()
    {
        string deep = @"HKLM\" + string.Join(@"\", Enumerable.Repeat("segment", 60));

        string name = RegBackupFile.NameFor(deep, Stamp, "deadbeef");

        Assert.True(name.Length <= 255, $"filename is {name.Length} chars");
        Assert.EndsWith("_20260710_010203_deadbeef.reg", name, StringComparison.Ordinal);
    }

    [Fact]
    public void PathFor_places_the_backup_in_the_backup_directory()
    {
        string path = RegBackupFile.PathFor(Path.Combine("C:", "Backups"), @"HKLM\Software\X");

        Assert.Equal(Path.Combine("C:", "Backups"), Path.GetDirectoryName(path));
        Assert.StartsWith("HKLM_Software_X_", Path.GetFileName(path), StringComparison.Ordinal);
    }
}
