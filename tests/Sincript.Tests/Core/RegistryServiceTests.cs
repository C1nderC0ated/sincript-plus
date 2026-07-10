using Sincript.Core;

namespace Sincript.Tests.Core;

/// <summary>
/// Hive expansion is a pure string transform (batch parity: :SafeRegAdd 1817-1823), so it needs
/// no registry. Calling it does not touch a P/Invoke, which is why this loads off Windows.
/// </summary>
public sealed class RegistryHivePathTests
{
    [Theory]
    [InlineData(@"HKLM\Software\X", @"HKEY_LOCAL_MACHINE\Software\X")]
    [InlineData(@"HKCU\Software\X", @"HKEY_CURRENT_USER\Software\X")]
    [InlineData(@"HKCR\.txt", @"HKEY_CLASSES_ROOT\.txt")]
    [InlineData(@"HKU\S-1-5-19", @"HKEY_USERS\S-1-5-19")]
    [InlineData(@"HKCC\System", @"HKEY_CURRENT_CONFIG\System")]
    public void Short_hive_names_expand_to_the_long_form_a_reg_file_requires(string input, string expected)
        => Assert.Equal(expected, RegistryService.ToFullHivePath(input));

    [Fact]
    public void An_already_long_path_passes_through_unchanged()
        => Assert.Equal(@"HKEY_LOCAL_MACHINE\Software\X",
                        RegistryService.ToFullHivePath(@"HKEY_LOCAL_MACHINE\Software\X"));

    [Fact]
    public void Hive_matching_is_case_insensitive_like_reg_exe()
        => Assert.Equal(@"HKEY_CURRENT_USER\Software\X", RegistryService.ToFullHivePath(@"hkcu\Software\X"));

    /// <summary>HKCU\ and HKU\ diverge at the third character, so neither shadows the other.</summary>
    [Fact]
    public void Hku_and_hkcu_do_not_shadow_each_other()
    {
        Assert.Equal(@"HKEY_USERS\X", RegistryService.ToFullHivePath(@"HKU\X"));
        Assert.Equal(@"HKEY_CURRENT_USER\X", RegistryService.ToFullHivePath(@"HKCU\X"));
    }

    [Fact]
    public void An_unrecognized_hive_is_rejected_rather_than_silently_mangled()
        => Assert.Throws<ArgumentException>(() => RegistryService.ToFullHivePath(@"HKXX\Nope"));
}
