using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Providers.Cardano;

public class QueryServiceIntegrationTests : IDisposable
{
    private readonly CardanoProvider _provider;
    private readonly IQueryService _queryService;
    private const string TestAddress = "addr_test1qpyzvufcwjfmfv5sld6cvnv9jxyt3fe0fh3kacgjlzu237ujrq209004flg53g7uwam0djh230lh5s7vrket4lgl5k3sxs7shy";

    public QueryServiceIntegrationTests()
    {
        _provider = new CardanoProvider(
            endpoint: "https://cardano-preview.utxorpc-m1.demeter.run",
            network: NetworkType.Preview,
            apiKey: "utxorpc14n9dqyezn3x52wf9wf8");
        _queryService = _provider;
    }

    [Fact]
    public async Task GetUtxosAsync_PreviewAddress_ReturnsUtxos()
    {
        // Act
        IReadOnlyList<Utxo> utxos = await _queryService.GetUtxosAsync(TestAddress);

        // Assert
        Assert.NotNull(utxos);

        // Log results
        foreach (var utxo in utxos)
        {
            Console.WriteLine($"TxHash: {utxo.TxHash}#{utxo.OutputIndex}");
            Console.WriteLine($"  Value: {utxo.Value} lovelace ({utxo.Value / 1_000_000m} ADA)");
            foreach (var asset in utxo.Assets)
            {
                Console.WriteLine($"  {asset.PolicyId}.{asset.AssetName}: {asset.Quantity}");
            }
        }
    }

    [Fact]
    public async Task GetBalanceAsync_PreviewAddress_ReturnsBalance()
    {
        // Act
        ulong balance = await _queryService.GetBalanceAsync(TestAddress);

        // Assert
        Console.WriteLine($"Balance: {balance} lovelace ({balance / 1_000_000m} ADA)");
        Assert.True(balance >= 0);
    }

    [Fact]
    public async Task GetAssetsAsync_PreviewAddress_ReturnsAggregatedAssets()
    {
        // Act
        IReadOnlyList<Asset> assets = await _queryService.GetAssetsAsync(TestAddress);

        // Assert
        Assert.NotNull(assets);

        var nfts = assets.Where(a => a.Quantity == 1).ToList();
        var tokens = assets.Where(a => a.Quantity > 1).ToList();

        Console.WriteLine($"Total unique assets: {assets.Count}");
        Console.WriteLine($"  NFTs (qty=1): {nfts.Count}");
        Console.WriteLine($"  Tokens (qty>1): {tokens.Count}");
        Console.WriteLine();

        Console.WriteLine("=== NFTs ===");
        foreach (var nft in nfts.Take(10))
        {
            Console.WriteLine($"  {nft.AssetName} (Policy: {nft.PolicyId[..16]}...)");
        }

        Console.WriteLine();
        Console.WriteLine("=== Tokens ===");
        foreach (var token in tokens.Take(10))
        {
            Console.WriteLine($"  {token.AssetName}: {token.Quantity}");
        }
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
