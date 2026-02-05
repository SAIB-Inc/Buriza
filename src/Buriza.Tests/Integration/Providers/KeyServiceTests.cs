using System.Text;
using Buriza.Core.Providers;
using Buriza.Data.Models.Enums;

namespace Buriza.Tests.Integration.Providers;

public class CardanoProviderTests : IDisposable
{
    private readonly CardanoProvider? _provider;
    private readonly CardanoTestConfig _config = IntegrationTestConfig.Instance.Cardano;

    private const string TestMnemonicString = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private static byte[] TestMnemonicBytes => Encoding.UTF8.GetBytes(TestMnemonicString);

    public CardanoProviderTests()
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

    #region DeriveAddress

    [SkippableFact]
    public async Task DeriveAddressAsync_ReturnsBech32()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string address = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.StartsWith("addr_test1", address);
        Assert.True(address.Length > 50);
    }

    [SkippableFact]
    public async Task DeriveAddressAsync_IsDeterministic()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string address1 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.Equal(address1, address2);
    }

    [SkippableFact]
    public async Task DeriveAddressAsync_DifferentIndex_ReturnsDifferent()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string address0 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address1 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 1);

        Assert.NotEqual(address0, address1);
    }

    [SkippableFact]
    public async Task DeriveAddressAsync_DifferentAccount_ReturnsDifferent()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string account0 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string account1 = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 1, 0);

        Assert.NotEqual(account0, account1);
    }

    [SkippableFact]
    public async Task DeriveAddressAsync_ChangeAddress_ReturnsDifferent()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string external = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: false);
        string change = await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, 0, isChange: true);

        Assert.NotEqual(external, change);
    }

    [SkippableTheory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(2, 10)]
    public async Task DeriveAddressAsync_VariousIndices_AllValid(int account, int index)
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        string address = await _provider!.DeriveAddressAsync(TestMnemonicBytes, account, index);

        Assert.StartsWith("addr_test1", address);
    }

    #endregion

    #region Integration

    [SkippableFact]
    public async Task MultipleAddresses_AllUnique()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        List<string> addresses = [];
        for (int i = 0; i < 10; i++)
        {
            addresses.Add(await _provider!.DeriveAddressAsync(TestMnemonicBytes, 0, i));
        }

        Assert.Equal(10, addresses.Distinct().Count());
    }

    [SkippableFact]
    public async Task DeriveAddress_DeterministicAcrossInstances()
    {
        Skip.If(!_config.IsConfigured, _config.SkipReason);

        NetworkType network = Enum.Parse<NetworkType>(_config.Network);
        using CardanoProvider provider1 = new(_config.Endpoint!, network, _config.ApiKey);
        using CardanoProvider provider2 = new(_config.Endpoint!, network, _config.ApiKey);

        string address1 = await provider1.DeriveAddressAsync(TestMnemonicBytes, 0, 0);
        string address2 = await provider2.DeriveAddressAsync(TestMnemonicBytes, 0, 0);

        Assert.Equal(address1, address2);
    }

    #endregion
}
