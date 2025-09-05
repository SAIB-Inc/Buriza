using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Buriza.UI.Resources;

namespace Buriza.UI.Data;

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
                    new TransactionAsset(Tokens.Ada, "ADA", -212.28m, 199.02m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", -45.75m, 18.30m),
                    new TransactionAsset(Tokens.Snek, "SNEK", -1200.50m, 89.15m)
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
                    new TransactionAsset(Tokens.Snek, "SNEK", 1500.00m, 245.80m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", 8500.00m, 125.75m),
                    new TransactionAsset(Tokens.Ada, "ADA", 75.25m, 70.50m)
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
                    new TransactionAsset(Tokens.Angel, "ANGEL", -85.50m, 32.15m),
                    new TransactionAsset(Tokens.Dexhunter, "DEXHUNTER", -250.80m, 145.60m),
                    new TransactionAsset(Tokens.Paylkoyn, "PAYLKOYN", -420.30m, 68.45m)
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
                    new TransactionAsset(Tokens.Hosky, "HOSKY", 25000.00m, 450.75m),
                    new TransactionAsset(Tokens.Ada, "ADA", 125.80m, 118.25m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 95.45m, 38.75m)
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
                    new TransactionAsset(Tokens.Dexhunter, "DEXHUNTER", -120.75m, 89.45m),
                    new TransactionAsset(Tokens.Ada, "ADA", -50.00m, 47.50m),
                    new TransactionAsset(Tokens.Snek, "SNEK", -750.20m, 55.80m)
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
                    new TransactionAsset(Tokens.Paylkoyn, "PAYLKOYN", 850.30m, 125.80m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 65.75m, 25.90m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", 12500.00m, 225.45m)
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