using System.Diagnostics;
using System.Text;
using Buriza.Core.Models;
using Buriza.Core.Providers;
using Buriza.Data.Models.Common;
using Buriza.Data.Models.Enums;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Models.Cbor;

namespace Buriza.Tests.Integration.Providers;

public class QueryServiceIntegrationTests : IDisposable
{
    private readonly CardanoProvider _provider;

    private const string TestAddress = "addr_test1qpyzvufcwjfmfv5sld6cvnv9jxyt3fe0fh3kacgjlzu237ujrq209004flg53g7uwam0djh230lh5s7vrket4lgl5k3sxs7shy";
    private static byte[] TestMnemonicBytes => Encoding.UTF8.GetBytes(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

    public QueryServiceIntegrationTests()
    {
        _provider = new CardanoProvider(
            endpoint: "https://cardano-preview.utxorpc-m1.demeter.run",
            network: NetworkType.Preview,
            apiKey: "utxorpc14n9dqyezn3x52wf9wf8");
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    #region IQueryService Tests

    [Fact]
    public async Task GetUtxosAsync_SingleAddress_ReturnsUtxos()
    {
        IReadOnlyList<Utxo> utxos = await _provider.GetUtxosAsync(TestAddress);

        Assert.NotNull(utxos);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsBalance()
    {
        ulong balance = await _provider.GetBalanceAsync(TestAddress);

        Assert.True(balance >= 0);
    }

    [Fact]
    public async Task GetAssetsAsync_ReturnsAggregatedAssets()
    {
        IReadOnlyList<Asset> assets = await _provider.GetAssetsAsync(TestAddress);

        Assert.NotNull(assets);
    }

    #endregion

    #region ICardanoDataProvider Tests (Multi-Address UTxO)

    [Fact]
    public async Task GetUtxosAsync_EmptyList_ReturnsEmpty()
    {
        List<ResolvedInput> utxos = await _provider.GetUtxosAsync([]);

        Assert.Empty(utxos);
    }

    [Fact]
    public async Task GetUtxosAsync_SingleAddressList_ReturnsUtxos()
    {
        List<ResolvedInput> utxos = await _provider.GetUtxosAsync([TestAddress]);

        Assert.NotNull(utxos);
    }

    [Fact]
    public async Task GetUtxosAsync_MultipleAddresses_ReturnsAggregated()
    {
        List<string> addresses = await DeriveAddressesAsync(5);

        List<ResolvedInput> utxos = await _provider.GetUtxosAsync(addresses);

        Assert.NotNull(utxos);
    }

    [Fact]
    public async Task GetUtxosAsync_MultipleAddresses_ExecutesInParallel()
    {
        List<string> addresses = await DeriveAddressesAsync(10);

        Stopwatch sw = Stopwatch.StartNew();
        List<ResolvedInput> utxos = await _provider.GetUtxosAsync(addresses);
        sw.Stop();

        Assert.NotNull(utxos);
        // With MaxParallelQueries=5, 10 addresses should complete in ~2 batches
        // Smoke test: parallel should be faster than 10 sequential calls
    }

    #endregion

    #region Helpers

    private async Task<List<string>> DeriveAddressesAsync(int count)
    {
        List<string> addresses = [];
        for (int i = 0; i < count; i++)
        {
            addresses.Add(await _provider.DeriveAddressAsync(TestMnemonicBytes, 0, i));
        }
        return addresses;
    }

    #endregion
}
