using Buriza.UI.Resources;

namespace Buriza.UI.Data.Dummy;

public record DappInfo(
    string Title,
    string Type,
    string Description,
    string Icon,
    bool IsFavorite = false
);

public static class DappData
{
    public static List<DappInfo> GetAllDapps()
    {
        return
        [
            new DappInfo(
                Title: "DexHunter",
                Type: "DeFi",
                Description: "Advanced DEX aggregator for optimal swaps across Cardano",
                Icon: Logos.Hunterdex,
                IsFavorite: true
            ),
            new DappInfo(
                Title: "Levvy",
                Type: "DeFi",
                Description: "NFT-backed lending and borrowing protocol",
                Icon: Logos.Levvy,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Minswap",
                Type: "DeFi",
                Description: "Multi-pool decentralized exchange on Cardano",
                Icon: Logos.Minswap,
                IsFavorite: true
            ),
            new DappInfo(
                Title: "DANZO",
                Type: "Gaming",
                Description: "Play-to-earn gaming platform with DeFi integration",
                Icon: Logos.Danzo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "SundaeSwap",
                Type: "DeFi",
                Description: "Native scalable DEX built for Cardano",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "JPG Store",
                Type: "NFT",
                Description: "The largest NFT marketplace on Cardano",
                Icon: Logos.Jpgstore,
                IsFavorite: true
            ),
            new DappInfo(
                Title: "Liqwid",
                Type: "DeFi",
                Description: "Decentralized lending and borrowing protocol",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "NMKR",
                Type: "NFT",
                Description: "No-code NFT minting and management platform",
                Icon: Logos.Indigo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Indigo",
                Type: "DeFi",
                Description: "Synthetic assets protocol on Cardano",
                Icon: Logos.Indigo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Cornucopias",
                Type: "Gaming",
                Description: "Play-to-earn blockchain-based metaverse game",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "MuesliSwap",
                Type: "DeFi",
                Description: "Order book and AMM hybrid DEX on Cardano",
                Icon: Logos.Minswap,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Book.io",
                Type: "NFT",
                Description: "Decentralized eBook and audiobook marketplace",
                Icon: Logos.Jpgstore,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "SummonPlatform",
                Type: "Governance",
                Description: "DAO tooling and on-chain voting for Cardano communities",
                Icon: Logos.Indigo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "DripDropz",
                Type: "Governance",
                Description: "Token distribution and governance participation platform",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "NEWM",
                Type: "Social",
                Description: "Decentralized music streaming and artist royalties platform",
                Icon: Logos.Jpgstore,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Wanchain",
                Type: "Bridge",
                Description: "Cross-chain bridge connecting Cardano with other blockchains",
                Icon: Logos.Indigo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Milkomeda",
                Type: "Bridge",
                Description: "EVM sidechain bridge for Cardano interoperability",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Charli3",
                Type: "Infrastructure",
                Description: "Decentralized oracle network for Cardano smart contracts",
                Icon: Logos.Jpgstore,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Blockfrost",
                Type: "Infrastructure",
                Description: "API and developer tools for building on Cardano",
                Icon: Logos.Indigo,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "Empowa",
                Type: "RealFi",
                Description: "Real-world property financing through decentralized lending",
                Icon: Logos.Splash,
                IsFavorite: false
            ),
            new DappInfo(
                Title: "World Mobile",
                Type: "RealFi",
                Description: "Decentralized mobile network and digital identity platform",
                Icon: Logos.Jpgstore,
                IsFavorite: true
            )
        ];
    }

    public static List<string> GetCategories()
    {
        return ["All", "Favorites", "DeFi", "NFT", "Gaming", "Governance", "Social", "Bridge", "Infrastructure", "RealFi"];
    }

    public static List<DappInfo> GetDappsByCategory(string category)
    {
        if (category == "All")
            return GetAllDapps();

        if (category == "Favorites")
            return GetFavoriteDapps();

        return GetAllDapps().Where(d => d.Type == category).ToList();
    }

    public static List<DappInfo> GetFavoriteDapps()
    {
        return GetAllDapps().Where(d => d.IsFavorite).ToList();
    }
}
