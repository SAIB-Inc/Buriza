using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;

namespace Buriza.Core.Interfaces.Chain;

/// <summary>
/// Chain-specific wallet operations: key derivation, transaction building, and signing.
/// Each chain (Cardano, Bitcoin, etc.) provides its own implementation.
/// </summary>
public interface IChainWallet
{
    /// <summary>Derives chain address data (address + chain-specific fields like staking address).</summary>
    Task<ChainAddressData> DeriveChainDataAsync(
        ReadOnlySpan<byte> mnemonic,
        ChainInfo chainInfo,
        int accountIndex,
        int addressIndex,
        bool isChange = false,
        CancellationToken ct = default);

    /// <summary>Builds an unsigned transaction from a request.</summary>
    Task<UnsignedTransaction> BuildTransactionAsync(
        string fromAddress,
        TransactionRequest request,
        IBurizaChainProvider provider,
        CancellationToken ct = default);

    /// <summary>
    /// Signs an unsigned transaction and returns the serialized signed transaction bytes.
    /// Derives the private key internally from mnemonic and zeros it after use.
    /// </summary>
    byte[] Sign(
        UnsignedTransaction unsignedTx,
        ReadOnlySpan<byte> mnemonic, ChainInfo chainInfo,
        int accountIndex, int addressIndex);
}
