using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
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
                TransactionFeeAda: 0.034m,
                Category: TransactionCategory.Contract
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
                TransactionFeeAda: 0.021m,
                Category: TransactionCategory.Mint
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
                TransactionFeeAda: 0.035m,
                Category: TransactionCategory.Mint
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
                TransactionFeeAda: 0.028m,
                Category: TransactionCategory.Contract
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
            ),

            new TransactionHistory(
                TransactionId: "1a2b3c4d...5e6f7g",
                Timestamp: DateTime.Now.AddDays(-3).AddHours(-2),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...6h8j2kgcqrqt3mm",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", -500.00m, 453.50m),
                    new TransactionAsset(Tokens.Meld, "MELD", -275.00m, 82.50m)
                ],
                CollateralAda: 175.00m,
                TransactionFeeAda: 0.025m
            ),

            new TransactionHistory(
                TransactionId: "8h9i0j1k...2l3m4n",
                Timestamp: DateTime.Now.AddDays(-3).AddHours(-7),
                FromAddress: "addr1q...4n6p8rgcqrqt1kk",
                ToAddress: "addr1q...8r7z4lgcqrqt9uu",
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", 1250.00m, 1133.75m),
                    new TransactionAsset(Tokens.Strike, "STRIKE", 500.00m, 545.00m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.018m
            ),

            new TransactionHistory(
                TransactionId: "5o6p7q8r...9s0t1u",
                Timestamp: DateTime.Now.AddDays(-4).AddHours(-4),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...2v4x6zgcqrqt0jj",
                Type: TransactionType.Mixed,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", -150.00m, 136.05m),
                    new TransactionAsset(Tokens.Djed, "DJED", 300.00m, 300.15m),
                    new TransactionAsset(Tokens.Usdc, "USDC", -100.00m, 100.30m)
                ],
                CollateralAda: 100.00m,
                TransactionFeeAda: 0.031m,
                Category: TransactionCategory.Mint
            ),

            new TransactionHistory(
                TransactionId: "2v3w4x5y...6z7a8b",
                Timestamp: DateTime.Now.AddDays(-4).AddHours(-9),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...9c1e3ggcqrqt8ii",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Snek, "SNEK", -1000.00m, 247.30m),
                    new TransactionAsset(Tokens.Hosky, "HOSKY", -5000.00m, 533.50m),
                    new TransactionAsset(Tokens.Ada, "ADA", -25.00m, 22.68m)
                ],
                CollateralAda: 50.00m,
                TransactionFeeAda: 0.029m
            ),

            new TransactionHistory(
                TransactionId: "9c0d1e2f...3g4h5i",
                Timestamp: DateTime.Now.AddDays(-5).AddHours(-1),
                FromAddress: "addr1q...7i9k1mgcqrqt6hh",
                ToAddress: "addr1q...8r7z4lgcqrqt9uu",
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Wmt, "WMT", 750.00m, 375.00m),
                    new TransactionAsset(Tokens.Ada, "ADA", 200.00m, 181.40m),
                    new TransactionAsset(Tokens.Iusd, "IUSD", 500.00m, 500.25m),
                    new TransactionAsset(Tokens.Angel, "ANGEL", 300.00m, 399.00m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.022m
            ),

            new TransactionHistory(
                TransactionId: "6j7k8l9m...0n1o2p",
                Timestamp: DateTime.Now.AddDays(-5).AddHours(-6),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...3q5s7ugcqrqt4gg",
                Type: TransactionType.Mixed,
                Assets: [
                    new TransactionAsset(Tokens.Sundae, "SUNDAE", -500.00m, 276.25m),
                    new TransactionAsset(Tokens.Nado, "NADO", 250.00m, 214.38m),
                    new TransactionAsset(Tokens.Ada, "ADA", 75.00m, 68.03m)
                ],
                CollateralAda: 125.00m,
                TransactionFeeAda: 0.033m
            ),

            new TransactionHistory(
                TransactionId: "3r4s5t6u...7v8w9x",
                Timestamp: DateTime.Now.AddDays(-6).AddHours(-3),
                FromAddress: "addr1q...1y3a5cgcqrqt2ff",
                ToAddress: "addr1q...8r7z4lgcqrqt9uu",
                Type: TransactionType.Received,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", 2500.00m, 2267.50m),
                    new TransactionAsset(Tokens.Dexhunter, "DEXHUNTER", 150.00m, 124.35m)
                ],
                CollateralAda: 0m,
                TransactionFeeAda: 0.017m
            ),

            new TransactionHistory(
                TransactionId: "0y1z2a3b...4c5d6e",
                Timestamp: DateTime.Now.AddDays(-7).AddHours(-5),
                FromAddress: "addr1q...8r7z4lgcqrqt9uu",
                ToAddress: "addr1q...5e7g9igcqrqt0ee",
                Type: TransactionType.Sent,
                Assets: [
                    new TransactionAsset(Tokens.Ada, "ADA", -750.00m, 680.25m),
                    new TransactionAsset(Tokens.Crawju, "CRAWJU", -400.00m, 162.80m),
                    new TransactionAsset(Tokens.Paylkoyn, "PAYLKOYN", -200.00m, 112.00m)
                ],
                CollateralAda: 250.00m,
                TransactionFeeAda: 0.038m
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