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
                    new TransactionAsset(Tokens.Strike, "STRIKE", -115.50m, 125.74m),
                    new TransactionAsset(Tokens.Meld, "MELD", -350.20m, 107.11m),
                    new TransactionAsset(Tokens.Sundae, "SUNDAE", -220.00m, 100.62m),
                    new TransactionAsset(Tokens.Snek, "SNEK", -500.00m, 123.65m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", -100.00m, 106.33m)
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
                    new TransactionAsset(Tokens.Usdc, "USDC", 150.00m, 150.45m),
                    new TransactionAsset(Tokens.Djed, "DJED", 185.75m, 185.86m),
                    new TransactionAsset(Tokens.Ada, "ADA", 175.25m, 168.22m),
                    new TransactionAsset(Tokens.Wmt, "WMT", 250.00m, 125.00m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 125.00m, 166.38m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.021m
            ),

            new TransactionHistory(
                TransactionId: "2f3a4b5c...6d7e8f",
                Timestamp: DateTime.Now.AddDays(-2).AddHours(-6),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...5x9z2mgcqrqt8pp",
                Type: TransactionType.Mixed,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", 45.50m, 41.25m),
                    new TransactionAsset(Tokens.Sundae, "SUNDAE", -200.00m, 110.50m),
                    new TransactionAsset(Tokens.Snek, "SNEK", 350.00m, 87.25m)
                ],
                CollateralAda: 150.00m,
                TransactionFeeAda: 0.035m
            ),
            
            new TransactionHistory(
                TransactionId: "8e1f2a3b...4c5d6e",
                Timestamp: DateTime.Now.AddHours(-5),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...9k2m5ngcqrqt7qq",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Sundae, "SUNDAE", -185.50m, 102.49m),
                    new TransactionAsset(Tokens.Crawju, "CRAWJU", -250.80m, 101.93m),
                    new TransactionAsset(Tokens.Nado, "NADO", -120.30m, 102.95m)
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
                    new TransactionAsset(Tokens.Wmt, "WMT", 185.00m, 162.50m),
                    new TransactionAsset(Tokens.Ada, "ADA", 125.80m, 114.05m),
                    new TransactionAsset(Tokens.Iusd, "IUSD", 195.45m, 195.61m)
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
                    new TransactionAsset(Tokens.Dexhunter, "DEXHUNTER", -120.75m, 100.14m),
                    new TransactionAsset(Tokens.Ada, "ADA", -150.00m, 145.33m),
                    new TransactionAsset(Tokens.Snek, "SNEK", -175.20m, 103.55m)
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
                    new TransactionAsset(Tokens.Paylkoyn, "PAYLKOYN", 185.30m, 103.83m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 165.75m, 174.49m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", 125.00m, 179.13m)
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