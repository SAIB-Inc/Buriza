using System.Text;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;

namespace Buriza.Core.Services;

/// <summary>
/// Static BIP-39 mnemonic operations. Chain-agnostic.
/// SECURITY: All mnemonic parameters use ReadOnlySpan&lt;byte&gt; to enable proper memory cleanup.
/// </summary>
public static class MnemonicService
{
    /// <summary>
    /// Generates a new mnemonic phrase as UTF-8 bytes.
    /// Caller must zero the returned bytes after use.
    /// </summary>
    public static byte[] Generate(int wordCount = 24)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return Encoding.UTF8.GetBytes(string.Join(" ", mnemonic.Words));
    }

    /// <summary>
    /// Validates a mnemonic phrase (UTF-8 bytes).
    /// </summary>
    public static bool Validate(ReadOnlySpan<byte> mnemonic)
    {
        try
        {
            string mnemonicStr = Encoding.UTF8.GetString(mnemonic);
            Mnemonic.Restore(mnemonicStr, English.Words);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
