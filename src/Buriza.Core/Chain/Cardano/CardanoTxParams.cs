using Buriza.Core.Models.Transaction;
using Chrysalis.Tx.Models;

namespace Buriza.Core.Chain.Cardano;

/// <summary>
/// Cardano transaction parameters for Chrysalis TransactionTemplateBuilder.
/// Built via <see cref="BurizaCardanoWallet.CreateParams"/>.
/// </summary>
public sealed class CardanoTxParams : ITransactionParameters
{
    public required Dictionary<string, (string address, bool isChange)> Parties { get; set; }
    public required TransactionRequest Request { get; init; }
}
