using System.Text;
using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// D3/F8. The tests that matter are the ones where kind-aware equality and byte equality
/// *disagree* — a value the batch would have rewritten (burying its own undo) and that we must
/// recognize as already-at-target, and its mirror: values that differ only in ways a normalizer
/// must never smooth over.
/// </summary>
public sealed class AlreadyAtTargetTests
{
    private const string Key = @"HKEY_CURRENT_USER\Software\Sincript";

    private static PriorValue Cur(RegKind kind, byte[] data) => new(Key, "n", Present: true, kind, data);
    private static byte[] Sz(string s) => Encoding.Unicode.GetBytes(s + "\0");
    private static byte[] Raw(string s) => Encoding.Unicode.GetBytes(s); // no terminator

    // ---------- the guards ----------

    [Fact]
    public void An_absent_value_is_never_at_target()
        => Assert.False(RegistryService.AlreadyAtTarget(PriorValue.Absent(Key, "n"), RegKind.Dword, [1, 0, 0, 0]));

    [Fact]
    public void A_kind_change_is_never_a_no_op()
        => Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("1")), RegKind.Dword, [1, 0, 0, 0]));

    // ---------- REG_SZ: the burial fix ----------

    [Fact]
    public void Identical_sz_is_at_target()
        => Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("on")), RegKind.Sz, Sz("on")));

    /// <summary>
    /// The whole point. Stored without its terminator, canonical target with one. Byte equality
    /// says "different", so the batch would rewrite it and snapshot the tweaked value as the
    /// prior state, destroying the true-original undo. Kind-aware equality says "already there".
    /// </summary>
    [Fact]
    public void Sz_missing_its_terminator_still_matches_a_canonical_target()
        => Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Raw("on")), RegKind.Sz, Sz("on")));

    [Fact]
    public void Sz_with_an_extra_terminator_still_matches()
        => Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("on\0")), RegKind.Sz, Sz("on")));

    [Fact]
    public void Different_sz_content_is_not_at_target()
        => Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("on")), RegKind.Sz, Sz("off")));

    /// <summary>Registry value *data* is case-sensitive; normalizing case would be a real bug.</summary>
    [Fact]
    public void Sz_comparison_is_case_sensitive()
        => Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("On")), RegKind.Sz, Sz("on")));

    [Fact]
    public void Sz_prefix_is_not_a_match()
        => Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, Sz("onward")), RegKind.Sz, Sz("on")));

    /// <summary>Documented in F8: both denote the empty string, so skipping the write buries nothing.</summary>
    [Fact]
    public void Zero_length_and_terminator_only_sz_both_mean_the_empty_string()
        => Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, []), RegKind.Sz, [0, 0]));

    [Fact]
    public void Odd_length_sz_falls_back_to_byte_equality()
    {
        Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, [0x61]), RegKind.Sz, [0x61]));
        Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Sz, [0x61]), RegKind.Sz, [0x61, 0x00]));
    }

    [Fact]
    public void Expand_sz_compares_the_literal_variable_not_its_expansion()
    {
        Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.ExpandSz, Raw("%SystemRoot%")), RegKind.ExpandSz, Sz("%SystemRoot%")));
        Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.ExpandSz, Sz(@"C:\Windows")), RegKind.ExpandSz, Sz("%SystemRoot%")));
    }

    // ---------- REG_MULTI_SZ ----------

    [Fact]
    public void Multi_sz_matches_regardless_of_how_the_tail_was_terminated()
    {
        byte[] canonical = [0x61, 0, 0, 0, 0x62, 0, 0, 0, 0, 0]; // "a","b" + final NUL
        byte[] shortTail = [0x61, 0, 0, 0, 0x62, 0, 0, 0];       // "a","b", no final NUL

        Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.MultiSz, shortTail), RegKind.MultiSz, canonical));
    }

    [Fact]
    public void Multi_sz_order_is_significant()
    {
        byte[] ab = [0x61, 0, 0, 0, 0x62, 0, 0, 0, 0, 0];
        byte[] ba = [0x62, 0, 0, 0, 0x61, 0, 0, 0, 0, 0];

        Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.MultiSz, ba), RegKind.MultiSz, ab));
    }

    [Fact]
    public void Multi_sz_missing_an_element_is_not_at_target()
    {
        byte[] ab = [0x61, 0, 0, 0, 0x62, 0, 0, 0, 0, 0];
        byte[] a = [0x61, 0, 0, 0, 0, 0];

        Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.MultiSz, a), RegKind.MultiSz, ab));
    }

    // ---------- opaque kinds stay byte-exact ----------

    [Fact]
    public void Dword_matches_only_on_identical_bytes()
    {
        Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.Dword, [1, 0, 0, 0]), RegKind.Dword, [1, 0, 0, 0]));
        Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Dword, [1, 0, 0, 0]), RegKind.Dword, [2, 0, 0, 0]));
    }

    /// <summary>A trailing zero byte in REG_BINARY is data. Trimming it here would be a silent
    /// corruption, which is why only the string kinds are normalized.</summary>
    [Fact]
    public void Binary_never_trims_a_trailing_zero_byte()
        => Assert.False(RegistryService.AlreadyAtTarget(Cur(RegKind.Binary, [1, 2]), RegKind.Binary, [1, 2, 0]));

    [Fact]
    public void Qword_is_byte_exact()
        => Assert.False(RegistryService.AlreadyAtTarget(
               Cur(RegKind.Qword, [1, 0, 0, 0, 0, 0, 0, 0]), RegKind.Qword, [1, 0, 0, 0, 0, 0, 0, 1]));

    [Fact]
    public void Empty_none_values_match()
        => Assert.True(RegistryService.AlreadyAtTarget(Cur(RegKind.None, []), RegKind.None, []));

    /// <summary>An unnamed kind is opaque, so it compares byte-exact rather than throwing.</summary>
    [Fact]
    public void An_unknown_kind_compares_byte_exact()
    {
        Assert.True(RegistryService.AlreadyAtTarget(Cur((RegKind)42, [0xFF]), (RegKind)42, [0xFF]));
        Assert.False(RegistryService.AlreadyAtTarget(Cur((RegKind)42, [0xFF]), (RegKind)42, [0xFE]));
    }
}
