using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public static class TransactionHistoryData
{
    public static List<TransactionHistory> GetDummyTransactions()
    {
        return
        [
            new TransactionHistory(
                TransactionId: "9f7a8b2c...1b93d2",
                Timestamp: DateTime.Now.AddMinutes(-23),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...17xpubmzsrqt9jp",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", -212.28m, 192.57m),
                    new TransactionAsset(Tokens.Strike, "STRIKE", -15.50m, 25.74m),
                    new TransactionAsset(Tokens.Meld, "MELD", -850.20m, 7.11m)
                ],
                CollateralAda: 433.00m,
                TransactionFeeAda: 0.034m
            ),
            
            new TransactionHistory(
                TransactionId: "3c2d9e4f...7a8b1c",
                Timestamp: DateTime.Now.AddHours(-2),
                FromAddress: "addr1q...5x9z2mgcqrqt8pp",
                ToAddress: "addr1q...8r7z4lgcqrqt9uu",
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Usdc, "USDC", 1500.00m, 1504.50m),
                    new TransactionAsset(Tokens.Djed, "DJED", 850.75m, 858.61m),
                    new TransactionAsset(Tokens.Ada, "ADA", 75.25m, 68.22m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.021m
            ),
            
            new TransactionHistory(
                TransactionId: "8e1f2a3b...4c5d6e",
                Timestamp: DateTime.Now.AddHours(-5),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...9k2m5ngcqrqt7qq",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Sundae, "SUNDAE", -485.50m, 2.49m),
                    new TransactionAsset(Tokens.Crawju, "CRAWJU", -1250.80m, 1.93m),
                    new TransactionAsset(Tokens.Nado, "NADO", -420.30m, 2.95m)
                ],
                CollateralAda: 125.00m,
                TransactionFeeAda: 0.028m
            ),
            
            new TransactionHistory(
                TransactionId: "7d8e9f0a...1b2c3d",
                Timestamp: DateTime.Now.AddDays(-1).AddHours(-3),
                FromAddress: "addr1q...2p4r6sgcqrqt5mm", 
                ToAddress: "addr1q...8r7z4lgcqrqt9uu",
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Wmt, "WMT", 1850.00m, 462.50m),
                    new TransactionAsset(Tokens.Ada, "ADA", 125.80m, 114.05m),
                    new TransactionAsset(Tokens.Iusd, "IUSD", 95.45m, 95.61m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.019m
            ),
            
            new TransactionHistory(
                TransactionId: "5b6c7d8e...9f0a1b",
                Timestamp: DateTime.Now.AddDays(-1).AddHours(-8),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...3v7x9ygcqrqt4nn",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Dexhunter, "DEXHUNTER", -120.75m, 0.14m),
                    new TransactionAsset(Tokens.Ada, "ADA", -50.00m, 45.33m),
                    new TransactionAsset(Tokens.Snek, "SNEK", -750.20m, 3.55m)
                ],
                CollateralAda: 200.00m,
                TransactionFeeAda: 0.042m
            ),
            
            new TransactionHistory(
                TransactionId: "4a5b6c7d...8e9f0a",
                Timestamp: DateTime.Now.AddDays(-2).AddHours(-1),
                FromAddress: "addr1q...1k4m7ngcqrqt2ll",
                ToAddress: "addr1q...8r7z4lgcqrqt9uu", 
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Paylkoyn, "PAYLKOYN", 850.30m, 3.83m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 65.75m, 174.49m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", 12500000.00m, 79.13m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.016m
            )
        ];
    }
    
    public static Dictionary<string, List<TransactionHistory>> GetTransactionsByDate()
    {
        return GetDummyTransactions()
            .GroupBy(t => t.Timestamp.Date)
            .ToDictionary(
                g => g.Key.ToString("MMMM dd, yyyy"),
                g => g.ToList()
            );
    }
}