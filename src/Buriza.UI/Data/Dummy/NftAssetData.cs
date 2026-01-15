using Buriza.Data.Models.Common;
using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public static class NftAssetData
{
    public static List<NftAsset> GetDummyNftAssets()
    {
        return
        [
            new NftAsset(Nfts.Nft1, "#0111", "PAYLS", 1.0f, 8.75f, 125.50f),
            new NftAsset(Nfts.Nft2, "#0212", "PAYLS", 1.0f, -12.30f, 89.25f),
            new NftAsset(Nfts.Nft3, "#0313", "PAYLS", 1.0f, 15.45f, 245.80f),
            new NftAsset(Nfts.Nft4, "#0414", "PAYLS", 1.0f, 22.10f, 67.90f),
            new NftAsset(Nfts.Nft5, "#0515", "PAYLS", 1.0f, -5.85f, 178.65f),
            new NftAsset(Nfts.Nft6, "#0616", "PAYLS", 1.0f, 18.90f, 312.40f),
            new NftAsset(Nfts.Nft1, "#0717", "PAYLS", 1.0f, -3.25f, 95.75f),
            new NftAsset(Nfts.Nft2, "#0818", "PAYLS", 1.0f, 9.80f, 134.20f),
            new NftAsset(Nfts.Nft3, "#0919", "PAYLS", 1.0f, 12.65f, 201.35f),
            new NftAsset(Nfts.Nft4, "#1020", "PAYLS", 1.0f, -7.45f, 156.90f),
            new NftAsset(Nfts.Nft5, "#1121", "PAYLS", 1.0f, 25.30f, 278.15f),
            new NftAsset(Nfts.Nft6, "#1222", "PAYLS", 1.0f, 4.55f, 87.60f),
            new NftAsset(Nfts.Nft1, "#1323", "PAYLS", 1.0f, -8.90f, 145.85f),
            new NftAsset(Nfts.Nft2, "#1424", "PAYLS", 1.0f, 14.20f, 198.40f),
            new NftAsset(Nfts.Nft3, "#1525", "PAYLS", 1.0f, 6.85f, 167.75f)
        ];
    }
}