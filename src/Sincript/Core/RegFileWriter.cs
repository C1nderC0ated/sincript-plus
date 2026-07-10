using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Sincript.Core;

/// <summary>
/// Registry value kinds as the raw REG_* DWORDs that <c>RegQueryValueEx</c> reports, rather than
/// the six <c>RegistryValueKind</c> models. A kind we have no name for is still a legal cast
/// (<c>(RegKind)42</c>) and still serializes as <c>hex(2a):</c> — D2 says *all* kinds back up
/// restorably, and a backup that drops what it cannot name is not a backup.
/// </summary>
internal enum RegKind : uint
{
    None = 0,
    Sz = 1,
    ExpandSz = 2,
    Binary = 3,
    Dword = 4,               // REG_DWORD_LITTLE_ENDIAN
    DwordBigEndian = 5,
    Link = 6,
    MultiSz = 7,
    ResourceList = 8,
    FullResourceDescriptor = 9,
    ResourceRequirementsList = 10,
    Qword = 11,              // REG_QWORD_LITTLE_ENDIAN
}

/// <summary>
/// The exact prior state of one registry value, captured before a tweak overwrites it.
/// <see cref="Data"/> is the untouched byte image from <c>RegQueryValueEx</c> — never a decoded
/// string — so an unterminated REG_SZ, an embedded NUL, or a kind .NET does not model all
/// survive the round trip. <see cref="ValueName"/> is <c>""</c> for a key's default value.
/// </summary>
internal sealed record PriorValue(
    string KeyPath,
    string ValueName,
    bool Present,
    RegKind Kind,
    byte[] Data)
{
    /// <summary>The value did not exist; restoring the backup must delete it ("name"=-).</summary>
    public static PriorValue Absent(string keyPath, string valueName)
        => new(keyPath, valueName, false, RegKind.None, []);
}

/// <summary>
/// Serializes a <see cref="PriorValue"/> to a single-value <c>.reg</c> file.
///
/// Replaces the batch's <c>:BackupValueLine</c> (PerfTweaks.cmd 1882-1916), which could only
/// restore REG_DWORD and ASCII REG_SZ; every other kind — and any non-ASCII string — got an
/// honest "not auto-restorable" comment instead of a backup. The cause is at line 1891: `echo`
/// writes through the console code page, so non-ASCII became mojibake. D2's fix is UTF-16LE
/// (what `reg export` itself emits), which is why <see cref="BuildBytes"/> carries a BOM.
///
/// The text form is chosen per kind for readability, but never at the cost of fidelity: a REG_SZ
/// whose bytes are not a cleanly terminated UTF-16 string falls back to <c>hex(1):</c> rather
/// than being normalized into something that would restore differently.
///
/// Wrapping matches `reg export`'s shape (80 columns, trailing "\", two-space continuations) but
/// the contract is *import compatibility*, not byte-parity: `reg import` accepts a continuation
/// after any comma.
/// </summary>
internal static class RegFileWriter
{
    public const string Header = "Windows Registry Editor Version 5.00";

    private const string Crlf = "\r\n";
    private const int MaxLineLength = 80;

    /// <summary>The file body, without the BOM. CRLF is hardcoded: `.reg` is a Windows format,
    /// and a dev host's Environment.NewLine must not leak into it.</summary>
    public static string BuildText(PriorValue value)
        => Header + Crlf + Crlf + "[" + value.KeyPath + "]" + Crlf + ValueLine(value) + Crlf + Crlf;

    /// <summary>UTF-16LE **with BOM** — the encoding `reg export` writes and the one that makes
    /// D2's non-ASCII REG_SZ restorable at all.</summary>
    public static byte[] BuildBytes(PriorValue value)
    {
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        byte[] preamble = encoding.GetPreamble();
        byte[] body = encoding.GetBytes(BuildText(value));

        var file = new byte[preamble.Length + body.Length];
        preamble.CopyTo(file, 0);
        body.CopyTo(file, preamble.Length);
        return file;
    }

    private static string ValueLine(PriorValue value)
    {
        // A key's default value is written "@=", never "\"\"=".
        string lead = value.ValueName.Length == 0 ? "@" : '"' + Escape(value.ValueName) + '"';

        // Batch parity (:BackupValueLine 1884-1887): an absent value restores as a deletion.
        if (!value.Present) return lead + "=-";

        return value.Kind switch
        {
            RegKind.Sz when TryDecodeSz(value.Data, out string text)
                => lead + "=\"" + Escape(text) + '"',

            RegKind.Dword when value.Data.Length == 4
                => lead + "=dword:" + BinaryPrimitives.ReadUInt32LittleEndian(value.Data)
                                                     .ToString("x8", CultureInfo.InvariantCulture),

            // REG_BINARY is spelled "hex:", not "hex(3):".
            RegKind.Binary => Hex(lead + "=hex:", value.Data),

            // Everything else — including a malformed SZ (hex(1)) or DWORD (hex(4)), and any kind
            // we cannot name — is a faithful hex dump of the bytes we already hold.
            _ => Hex(lead + $"=hex({(uint)value.Kind:x}):", value.Data),
        };
    }

    /// <summary>Both value names and REG_SZ data escape a backslash and a double quote, in that
    /// order — reversing it would double-escape the backslash a quote-escape just introduced.</summary>
    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// True only when <paramref name="data"/> is a well-formed UTF-16LE string: even length,
    /// exactly one terminating NUL unit, and no embedded NUL. Anything else is left to the
    /// caller's hex fallback, because "".Replace()-ing it into a quoted string would silently
    /// change what a restore writes back.
    /// </summary>
    private static bool TryDecodeSz(byte[] data, out string text)
    {
        text = "";
        if (data.Length < 2 || data.Length % 2 != 0) return false;
        if (data[^2] != 0 || data[^1] != 0) return false;

        for (int i = 0; i + 1 < data.Length - 2; i += 2)
            if (data[i] == 0 && data[i + 1] == 0) return false; // embedded NUL

        text = Encoding.Unicode.GetString(data, 0, data.Length - 2);
        return true;
    }

    /// <summary>Comma-separated lowercase octets, wrapped at 80 columns with a trailing "\" and
    /// a two-space continuation indent. Empty data legally yields a bare "…=hex:".</summary>
    private static string Hex(string lead, byte[] data)
    {
        var sb = new StringBuilder(lead);
        int column = lead.Length;

        for (int i = 0; i < data.Length; i++)
        {
            string token = data[i].ToString("x2", CultureInfo.InvariantCulture)
                         + (i < data.Length - 1 ? "," : "");

            // Reserve one column for the "\" this line would need if it continues.
            if (i > 0 && column + token.Length > MaxLineLength - 1)
            {
                sb.Append('\\').Append(Crlf).Append("  ");
                column = 2;
            }

            sb.Append(token);
            column += token.Length;
        }

        return sb.ToString();
    }
}
