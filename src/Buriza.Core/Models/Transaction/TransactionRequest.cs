using Chrysalis.Tx.Models;

namespace Buriza.Core.Models.Transaction;

public record TransactionRequest : ITransactionParameters
{
    public required string FromAddress { get; init; }
    public required List<TransactionRecipient> Recipients { get; init; }
    public string? Message { get; init; }
    public Dictionary<ulong, object>? Metadata { get; init; }

    public Dictionary<string, (string address, bool isChange)> Parties
    {
        get
        {
            Dictionary<string, (string address, bool isChange)> parties = new()
            {
                ["sender"] = (FromAddress, false),
                ["change"] = (FromAddress, true)
            };

            for (int i = 0; i < Recipients.Count; i++)
            {
                parties[$"recipient_{i}"] = (Recipients[i].Address, false);
            }

            return parties;
        }
        set { } // Required by ITransactionParameters interface
    }
}
