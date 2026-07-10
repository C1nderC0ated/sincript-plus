using System.Text;
using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// D2's contract: every value kind backs up restorably, and nothing is silently normalized on
/// the way out. These are pure byte -> text assertions, so they run on any dev host; the
/// write -> `reg import` -> read-back round trip that proves Windows agrees lives in CI.
/// </summary>
public sealed class RegFileWriterTests
{
    private const string Key = @"HKEY_CURRENT_USER\Software\Sincript";

    private static PriorValue Val(RegKind kind, byte[] data, string name = "n")
        => new(Key, name, Present: true, kind, data);

    /// <summary>A REG_SZ as the registry actually stores it: UTF-16LE plus a NUL terminator.</summary>
    private static byte[] SzBytes(string s) => Encoding.Unicode.GetBytes(s + "\0");

    /// <summary>The value line is line 4: header, blank, [key], value.</summary>
    private static string LineOf(PriorValue v) => RegFileWriter.BuildText(v).Split("\r\n")[3];

    // ---------- file shell ----------

    [Fact]
    public void Text_opens_with_the_v5_header_a_blank_line_and_the_key()
    {
        string[] lines = RegFileWriter.BuildText(Val(RegKind.Dword, [1, 0, 0, 0])).Split("\r\n");

        Assert.Equal("Windows Registry Editor Version 5.00", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal($"[{Key}]", lines[2]);
    }

    /// <summary>`.reg` is a Windows format; a Unix dev host's Environment.NewLine must not leak
    /// into it. Every LF in the file has to be preceded by a CR.</summary>
    [Fact]
    public void Text_uses_crlf_regardless_of_the_dev_hosts_newline()
    {
        string text = RegFileWriter.BuildText(Val(RegKind.Dword, [1, 0, 0, 0]));

        Assert.Contains("\r\n", text, StringComparison.Ordinal);
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '\n')
                Assert.True(i > 0 && text[i - 1] == '\r', $"bare LF at index {i}");
    }

    /// <summary>The whole point of D2: UTF-16LE with a BOM, as `reg export` writes.</summary>
    [Fact]
    public void Bytes_begin_with_the_utf16le_bom()
    {
        byte[] file = RegFileWriter.BuildBytes(Val(RegKind.Sz, SzBytes("x")));

        Assert.Equal(0xFF, file[0]);
        Assert.Equal(0xFE, file[1]);
        Assert.Equal((byte)'W', file[2]);   // 'W' of "Windows", low byte first
        Assert.Equal(0x00, file[3]);
    }

    // ---------- absent / default value ----------

    [Fact]
    public void Absent_value_restores_as_a_deletion()
        => Assert.Equal("\"n\"=-", LineOf(PriorValue.Absent(Key, "n")));

    [Fact]
    public void Default_value_is_written_with_an_at_sign_not_an_empty_name()
        => Assert.Equal("@=-", LineOf(PriorValue.Absent(Key, "")));

    [Fact]
    public void Default_value_with_data_is_written_with_an_at_sign()
        => Assert.Equal("@=\"hi\"", LineOf(Val(RegKind.Sz, SzBytes("hi"), name: "")));

    // ---------- REG_SZ ----------

    [Fact]
    public void Sz_is_written_as_a_quoted_string()
        => Assert.Equal("\"n\"=\"hello\"", LineOf(Val(RegKind.Sz, SzBytes("hello"))));

    [Fact]
    public void Sz_escapes_backslashes_and_quotes_in_data()
        => Assert.Equal("\"n\"=\"C:\\\\Program Files\\\\a\\\"b\\\"\"",
                        LineOf(Val(RegKind.Sz, SzBytes("C:\\Program Files\\a\"b\""))));

    [Fact]
    public void Value_name_is_escaped_the_same_way_as_data()
        => Assert.Equal("\"a\\\\b\\\"c\"=\"x\"",
                        LineOf(Val(RegKind.Sz, SzBytes("x"), name: "a\\b\"c")));

    /// <summary>The batch declined here (line 1903) because `echo` mangled non-ASCII into the
    /// console code page. UTF-16LE makes it a normal, restorable value.</summary>
    [Fact]
    public void Sz_round_trips_non_ascii_that_the_batch_refused_to_back_up()
    {
        var value = Val(RegKind.Sz, SzBytes("Диспетчер задач"));

        Assert.Equal("\"n\"=\"Диспетчер задач\"", LineOf(value));

        // and it really is those code units in the file, not '?' replacements
        byte[] file = RegFileWriter.BuildBytes(value);
        Assert.True(Contains(file, Encoding.Unicode.GetBytes("Диспетчер задач")),
                    "the UTF-16LE file must carry the Cyrillic code units verbatim");
    }

    [Fact]
    public void Empty_sz_is_a_terminator_only_value_and_writes_as_empty_quotes()
        => Assert.Equal("\"n\"=\"\"", LineOf(Val(RegKind.Sz, [0, 0])));

    /// <summary>Fidelity over prettiness: bytes that are not a clean UTF-16 string must not be
    /// laundered into a quoted string that would restore differently.</summary>
    [Fact]
    public void Unterminated_sz_falls_back_to_hex1_rather_than_being_normalized()
        => Assert.Equal("\"n\"=hex(1):68,00,69,00", LineOf(Val(RegKind.Sz, Encoding.Unicode.GetBytes("hi"))));

    [Fact]
    public void Sz_with_an_embedded_nul_falls_back_to_hex1()
        => Assert.Equal("\"n\"=hex(1):61,00,00,00,62,00,00,00",
                        LineOf(Val(RegKind.Sz, [0x61, 0, 0, 0, 0x62, 0, 0, 0])));

    [Fact]
    public void Odd_length_sz_falls_back_to_hex1()
        => Assert.Equal("\"n\"=hex(1):61", LineOf(Val(RegKind.Sz, [0x61])));

    // ---------- REG_DWORD ----------

    [Fact]
    public void Dword_is_eight_lowercase_hex_digits_read_little_endian()
        => Assert.Equal("\"n\"=dword:00000001", LineOf(Val(RegKind.Dword, [0x01, 0x00, 0x00, 0x00])));

    [Fact]
    public void Dword_renders_the_numeric_value_not_the_byte_order()
        => Assert.Equal("\"n\"=dword:deadbeef", LineOf(Val(RegKind.Dword, [0xEF, 0xBE, 0xAD, 0xDE])));

    [Fact]
    public void Dword_max_value_does_not_overflow_or_sign_flip()
        => Assert.Equal("\"n\"=dword:ffffffff", LineOf(Val(RegKind.Dword, [0xFF, 0xFF, 0xFF, 0xFF])));

    [Fact]
    public void Dword_of_the_wrong_length_falls_back_to_hex4()
        => Assert.Equal("\"n\"=hex(4):01,00", LineOf(Val(RegKind.Dword, [1, 0])));

    // ---------- hex kinds ----------

    [Fact]
    public void Qword_is_hex_b_with_little_endian_bytes_preserved_verbatim()
        => Assert.Equal("\"n\"=hex(b):01,02,03,04,05,06,07,08",
                        LineOf(Val(RegKind.Qword, [1, 2, 3, 4, 5, 6, 7, 8])));

    [Fact]
    public void Binary_is_spelled_hex_not_hex3()
        => Assert.Equal("\"n\"=hex:de,ad", LineOf(Val(RegKind.Binary, [0xDE, 0xAD])));

    [Fact]
    public void Empty_binary_is_a_bare_hex_prefix()
        => Assert.Equal("\"n\"=hex:", LineOf(Val(RegKind.Binary, [])));

    [Fact]
    public void None_is_hex0()
        => Assert.Equal("\"n\"=hex(0):", LineOf(Val(RegKind.None, [])));

    [Fact]
    public void Expand_sz_is_hex2_over_the_raw_bytes_so_percent_vars_are_never_expanded()
        => Assert.Equal("\"n\"=hex(2):25,00,50,00,00,00",
                        LineOf(Val(RegKind.ExpandSz, [0x25, 0, 0x50, 0, 0, 0])));

    /// <summary>MULTI_SZ: each string NUL-terminated, then a final NUL. Dumping the captured
    /// bytes means we cannot get that terminator arithmetic wrong.</summary>
    [Fact]
    public void Multi_sz_is_hex7_with_every_terminator_intact()
        => Assert.Equal("\"n\"=hex(7):61,00,00,00,62,00,00,00,00,00",
                        LineOf(Val(RegKind.MultiSz, [0x61, 0, 0, 0, 0x62, 0, 0, 0, 0, 0])));

    /// <summary>D2: a kind we have no name for still backs up, as hex(2a).</summary>
    [Fact]
    public void Unknown_kind_serializes_by_its_raw_dword()
        => Assert.Equal("\"n\"=hex(2a):ff", LineOf(Val((RegKind)42, [0xFF])));

    // ---------- line wrapping ----------

    [Fact]
    public void Long_hex_wraps_at_eighty_columns_with_backslash_continuations()
    {
        byte[] data = new byte[200];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

        string[] lines = RegFileWriter.BuildText(Val(RegKind.Binary, data))
                                      .Split("\r\n")[3..]
                                      .Where(l => l.Length > 0)
                                      .ToArray();

        Assert.True(lines.Length > 1, "200 bytes must wrap onto several lines");

        foreach (string line in lines)
            Assert.True(line.Length <= 80, $"line exceeds 80 columns ({line.Length}): {line}");

        // every line but the last announces its continuation; every continuation is indented
        foreach (string line in lines[..^1])
            Assert.EndsWith("\\", line, StringComparison.Ordinal);
        foreach (string line in lines[1..])
            Assert.StartsWith("  ", line, StringComparison.Ordinal);

        Assert.DoesNotContain("\\", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Wrapped_hex_still_carries_every_byte_in_order()
    {
        byte[] data = new byte[200];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

        string body = string.Concat(
            RegFileWriter.BuildText(Val(RegKind.Binary, data))
                         .Split("\r\n")[3..]
                         .Select(l => l.Trim().TrimEnd('\\')));

        string[] octets = body["\"n\"=hex:".Length..].Split(',');
        Assert.Equal(200, octets.Length);
        Assert.Equal("00", octets[0]);
        Assert.Equal("c7", octets[199]);
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool hit = true;
            for (int j = 0; j < needle.Length && hit; j++)
                if (haystack[i + j] != needle[j]) hit = false;
            if (hit) return true;
        }
        return false;
    }
}
