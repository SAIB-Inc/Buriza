using Buriza.Data.Models.Common;
using Buriza.UI.Resources;

namespace Buriza.UI.Data;

public static class TokenAssetData
{
    public static List<TokenAsset> GetDummyTokenAssets()
    {
        return
        [
            new TokenAsset(Tokens.Ada, "ADA", 12.18f, 5304.01f, 1652.31f),
            new TokenAsset(Tokens.Angel, "ANGEL", -5.42f, 250.75f, 89.23f),
            new TokenAsset(Tokens.Dexhunter, "DEXHUNTER", 8.96f, 1890.32f, 421.78f),
            new TokenAsset(Tokens.Hosky, "HOSKY", 25.73f, 15000.00f, 2350.45f),
            new TokenAsset(Tokens.Paylkoyn, "PAYLKOYN", -12.85f, 750.60f, 125.89f),
            new TokenAsset(Tokens.Snek, "SNEK", 18.42f, 3200.15f, 890.67f),
            new TokenAsset(Tokens.Ada, "USDC", 0.12f, 1000.00f, 999.88f),
            new TokenAsset(Tokens.Angel, "USDT", -0.05f, 2500.50f, 2498.25f),
            new TokenAsset(Tokens.Dexhunter, "DJED", 1.85f, 450.30f, 458.63f),
            new TokenAsset(Tokens.Hosky, "IUSD", -0.78f, 850.25f, 843.61f),
            new TokenAsset(Tokens.Paylkoyn, "COPI", 45.67f, 125.40f, 182.67f),
            new TokenAsset(Tokens.Snek, "WMT", -8.23f, 320.80f, 294.41f),
            new TokenAsset(Tokens.Ada, "MELD", 15.94f, 680.90f, 789.45f),
            new TokenAsset(Tokens.Angel, "SUNDAE", 22.31f, 1250.75f, 1529.59f),
            new TokenAsset(Tokens.Dexhunter, "MIN", 7.89f, 920.45f, 992.98f)
        ];
    }
}