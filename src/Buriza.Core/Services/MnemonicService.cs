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
    public static byte[] Generate(int wordCount = 24)
    {
        Mnemonic mnemonic = Mnemonic.Generate(English.Words, wordCount);
        return Encoding.UTF8.GetBytes(string.Join(" ", mnemonic.Words));
    }

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
