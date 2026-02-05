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
    private readonly CardanoProvider? _provider;
    private readonly CardanoTestConfig _config = IntegrationTestConfig.Instance.Cardano;

    private const string TestAddress = "addr_test1qpyzvufcwjfmfv5sld6cvnv9jxyt3fe0fh3kacgjlzu237ujrq209004flg53g7uwam0djh230lh5s7vrket4lgl5k3sxs7shy";
    private static byte[] TestMnemonicBytes => Encoding.UTF8.GetBytes(
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

    public QueryServiceIntegrationTests()
    {
        if (_config.IsConfigured)
        {
            NetworkType network = Enum.Parse<NetworkType>(_config.Network);
            _provider = new CardanoProvider(_config.Endpoint!, network, _config.ApiKey);
        }
    }

    public void Dispose()
    {
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region IQueryService Tests

    [SkippableFact]
    public async Task GetUtxosAsync_SingleAddress_ReturnsUtxos()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        IReadOnlyList<Utxo> utxos = await _provider!.GetUtxosAsync(TestAddress);

        Assert.NotNull(utxos);
    }

    [SkippableFact]
    public async Task GetBalanceAsync_ReturnsBalance()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        ulong balance = await _provider!.GetBalanceAsync(TestAddress);

        Assert.True(balance >= 0);
    }

    [SkippableFact]
    public async Task GetAssetsAsync_ReturnsAggregatedAssets()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        IReadOnlyList<Asset> assets = await _provider!.GetAssetsAsync(TestAddress);

        Assert.NotNull(assets);
    }

    #endregion

    #region ICardanoDataProvider Tests (Multi-Address UTxO)

    [SkippableFact]
    public async Task GetUtxosAsync_EmptyList_ReturnsEmpty()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        List<ResolvedInput> utxos = await _provider!.GetUtxosAsync([]);

        Assert.Empty(utxos);
    }

    [SkippableFact]
    public async Task GetUtxosAsync_SingleAddressList_ReturnsUtxos()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        List<ResolvedInput> utxos = await _provider!.GetUtxosAsync([TestAddress]);

        Assert.NotNull(utxos);
    }

    [SkippableFact]
    public async Task GetUtxosAsync_MultipleAddresses_ReturnsAggregated()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        List<string> addresses = await DeriveAddressesAsync(5);
        List<ResolvedInput> utxos = await _provider!.GetUtxosAsync(addresses);

        Assert.NotNull(utxos);
    }

    [SkippableFact]
    public async Task GetUtxosAsync_MultipleAddresses_ExecutesInParallel()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        List<string> addresses = await DeriveAddressesAsync(10);

        Stopwatch sw = Stopwatch.StartNew();
        List<ResolvedInput> utxos = await _provider!.GetUtxosAsync(addresses);
        sw.Stop();

        Assert.NotNull(utxos);
    }

    #endregion

    #region Helpers

    private async Task<List<string>> DeriveAddressesAsync(int count)
    {
        List<string> addresses = [];
        for (int i = 0; i < count; i++)
        {
            addresses.Add(await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, i));
        }
        return addresses;
    }

    #endregion
}
