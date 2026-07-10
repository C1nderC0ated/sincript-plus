using System.Runtime.InteropServices;

namespace Sincript.Core;

/// <summary>
/// The Win32 seam. Everything above this class works on <see cref="PriorValue"/> byte images and
/// is testable on any dev host; only what is in here needs a real Windows kernel underneath.
///
/// Reads go through <c>RegQueryValueEx</c> rather than <c>Microsoft.Win32.Registry</c> because a
/// backup must capture the value as stored, not as .NET chooses to model it (F4). That also
/// removes any chance of the <c>REG_EXPAND_SZ</c> auto-expansion trap: there is no string layer
/// to expand anything (F7).
///
/// The process is 64-bit by contract (plan §2, Program.cs asserts it), so the default registry
/// view is the 64-bit one and <c>WOW6432Node</c> paths resolve exactly as they did under the
/// batch's 64-bit cmd. No KEY_WOW64_* flag is passed, deliberately.
/// </summary>
internal static class RegistryService
{
    // Predefined HKEYs are LONG-typed constants that sign-extend to 64-bit (0xFFFFFFFF80000002),
    // which is why these go through int before widening to nint.
    private static readonly nint HkeyClassesRoot = unchecked((int)0x80000000);
    private static readonly nint HkeyCurrentUser = unchecked((int)0x80000001);
    private static readonly nint HkeyLocalMachine = unchecked((int)0x80000002);
    private static readonly nint HkeyUsers = unchecked((int)0x80000003);
    private static readonly nint HkeyCurrentConfig = unchecked((int)0x80000005);

    private const int ErrorSuccess = 0;
    private const int ErrorFileNotFound = 2;
    private const uint KeyRead = 0x20019;

    /// <summary>Batch parity (:SafeRegAdd 1817-1823): a `.reg` file needs the long hive name.
    /// Accepts either spelling and always yields the long one. Pure — no registry access.</summary>
    public static string ToFullHivePath(string keyPath)
    {
        foreach ((string shortName, string fullName, _) in Hives)
        {
            if (keyPath.StartsWith(fullName, StringComparison.OrdinalIgnoreCase)) return keyPath;
            if (keyPath.StartsWith(shortName, StringComparison.OrdinalIgnoreCase))
                return fullName + keyPath[shortName.Length..];
        }
        throw new ArgumentException($"Unrecognized registry hive in \"{keyPath}\".", nameof(keyPath));
    }

    /// <summary>The five hives the batch expanded, short spelling to long. No prefix here is a
    /// prefix of another (`HKCU\` vs `HKU\` differ at the third character), so order is free.</summary>
    private static readonly (string Short, string Full, nint Handle)[] Hives =
    [
        (@"HKLM\", @"HKEY_LOCAL_MACHINE\", HkeyLocalMachine),
        (@"HKCU\", @"HKEY_CURRENT_USER\", HkeyCurrentUser),
        (@"HKCR\", @"HKEY_CLASSES_ROOT\", HkeyClassesRoot),
        (@"HKCC\", @"HKEY_CURRENT_CONFIG\", HkeyCurrentConfig),
        (@"HKU\", @"HKEY_USERS\", HkeyUsers),
    ];

    private static (nint Root, string SubKey, string FullPath) Resolve(string keyPath)
    {
        string full = ToFullHivePath(keyPath);
        foreach ((_, string fullName, nint handle) in Hives)
            if (full.StartsWith(fullName, StringComparison.OrdinalIgnoreCase))
                return (handle, full[fullName.Length..], full);

        throw new ArgumentException($"Unrecognized registry hive in \"{keyPath}\".", nameof(keyPath));
    }

    /// <summary>
    /// The exact prior state of one value, or <see cref="PriorValue.Absent"/> when either the key
    /// or the value does not exist — the two cases the batch collapsed into "not defined _ln",
    /// and which both restore as a deletion.
    /// </summary>
    public static PriorValue Capture(string keyPath, string valueName)
    {
        (nint root, string subKey, string fullPath) = Resolve(keyPath);

        if (RegOpenKeyEx(root, subKey, 0, KeyRead, out nint key) != ErrorSuccess)
            return PriorValue.Absent(fullPath, valueName);

        try
        {
            // Size probe first: a null buffer asks the API how many bytes the value occupies.
            uint size = 0;
            int rc = RegQueryValueSize(key, valueName, 0, out uint kind, 0, ref size);
            if (rc == ErrorFileNotFound) return PriorValue.Absent(fullPath, valueName);
            if (rc != ErrorSuccess) return PriorValue.Absent(fullPath, valueName);

            if (size == 0) return new PriorValue(fullPath, valueName, true, (RegKind)kind, []);

            var data = new byte[size];
            rc = RegQueryValueData(key, valueName, 0, out kind, data, ref size);
            if (rc != ErrorSuccess) return PriorValue.Absent(fullPath, valueName);

            // A concurrent writer can shrink the value between the two calls; trust the second.
            if (size != data.Length) Array.Resize(ref data, (int)size);

            return new PriorValue(fullPath, valueName, true, (RegKind)kind, data);
        }
        finally
        {
            RegCloseKey(key);
        }
    }

    // Classic DllImport, matching Hardware.cs: NativeAOT compiles the marshalling stubs ahead of
    // time, and the trim/AOT analyzers are clean on these signatures.

    [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyEx(nint hKey, string subKey, uint options, uint samDesired, out nint result);

    /// <summary>lpData = 0 (null): asks only for the byte count and the type.</summary>
    [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueSize(nint hKey, string? valueName, nint reserved, out uint type, nint lpData, ref uint cbData);

    [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueData(nint hKey, string? valueName, nint reserved, out uint type, byte[] lpData, ref uint cbData);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegCloseKey(nint hKey);
}
