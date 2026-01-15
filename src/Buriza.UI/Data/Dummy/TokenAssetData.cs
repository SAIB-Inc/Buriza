using Buriza.Data.Models.Common;
using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public static class TokenAssetData
{
    public static List<TokenAsset> GetDummyTokenAssets()
    {
        return
        [
            new TokenAsset(Tokens.Ada, "ADA", 12.18f, 5304.01f, 0.9066f),
            new TokenAsset(Tokens.Angel, "ANGEL", -5.42f, 250.75f, 2.653f),
            new TokenAsset(Tokens.Dexhunter, "DEXHUNTER", 8.96f, 1890.32f, 0.00116f),
            new TokenAsset(Tokens.Hosky, "HOSKY", 25.73f, 15000.00f, 0.00633f),
            new TokenAsset(Tokens.Paylkoyn, "PAYLKOYN", -12.85f, 750.60f, 0.0045f),
            new TokenAsset(Tokens.Snek, "SNEK", 18.42f, 3200.15f, 0.00473f),
            new TokenAsset(Tokens.Usdc, "USDC", 0.12f, 1000.00f, 1.003f),
            new TokenAsset(Tokens.Crawju, "CRAWJU", 7.89f, 920.45f, 0.00154f),
            new TokenAsset(Tokens.Strike, "STRIKE", -0.05f, 2500.50f, 1.661f),
            new TokenAsset(Tokens.Djed, "DJED", 1.85f, 450.30f, 1.009f),
            new TokenAsset(Tokens.Iusd, "IUSD", -0.78f, 850.25f, 1.0017f),
            new TokenAsset(Tokens.Nado, "NADO", 45.67f, 125.40f, 0.00702f),
            new TokenAsset(Tokens.Wmt, "WMT", -8.23f, 320.80f, 0.250f),
            new TokenAsset(Tokens.Meld, "MELD", 15.94f, 680.90f, 0.00837f),
            new TokenAsset(Tokens.Sundae, "SUNDAE", 22.31f, 1250.75f, 0.00514f)
        ];
    }
}