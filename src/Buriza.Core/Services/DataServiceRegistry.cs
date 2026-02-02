using System.Security.Cryptography;
using System.Text;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Storage;
using Buriza.Core.Models;
using Buriza.Data.Models.Enums;

namespace Buriza.Core.Services;

/// <summary>
/// Registry for data service configurations.
/// Manages both persistent storage and session cache for custom endpoints/API keys.
/// </summary>
public class DataServiceRegistry(
    IWalletStorage storage,
    ISessionService sessionService) : IDataServiceRegistry
{
    private readonly IWalletStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly ISessionService _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

    public async Task<DataServiceConfig?> GetConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        return await _storage.GetDataServiceConfigAsync(serviceType, ct);
    }

    public async Task SetConfigAsync(DataServiceType serviceType, string? endpoint, string? apiKey, string password, string? name = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(endpoint) && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid endpoint URL", nameof(endpoint));

        DataServiceConfig config = new()
        {
            ServiceType = serviceType,
            Endpoint = endpoint,
            HasCustomApiKey = !string.IsNullOrEmpty(apiKey),
            Name = name
        };
        await _storage.SaveDataServiceConfigAsync(config, ct);

        // Cache endpoint in session for immediate use
        if (!string.IsNullOrEmpty(endpoint))
            _sessionService.SetDataServiceEndpoint(serviceType, endpoint);
        else
            _sessionService.ClearDataServiceEndpoint(serviceType);

        // Save encrypted API key if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            await _storage.SaveDataServiceApiKeyAsync(serviceType, apiKey, password, ct);
            _sessionService.SetDataServiceApiKey(serviceType, apiKey);
        }
        else
        {
            await _storage.DeleteDataServiceApiKeyAsync(serviceType, ct);
            _sessionService.ClearDataServiceApiKey(serviceType);
        }
    }

    public async Task ClearConfigAsync(DataServiceType serviceType, CancellationToken ct = default)
    {
        await _storage.DeleteDataServiceConfigAsync(serviceType, ct);
        _sessionService.ClearDataServiceEndpoint(serviceType);
        _sessionService.ClearDataServiceApiKey(serviceType);
    }

    public async Task LoadConfigAsync(DataServiceType serviceType, string password, CancellationToken ct = default)
    {
        DataServiceConfig? config = await _storage.GetDataServiceConfigAsync(serviceType, ct);
        if (config?.Endpoint != null)
            _sessionService.SetDataServiceEndpoint(serviceType, config.Endpoint);

        if (config?.HasCustomApiKey == true)
        {
            byte[]? apiKeyBytes = await _storage.UnlockDataServiceApiKeyAsync(serviceType, password, ct);
            if (apiKeyBytes != null)
            {
                try
                {
                    string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
                    _sessionService.SetDataServiceApiKey(serviceType, apiKey);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(apiKeyBytes);
                }
            }
        }
    }

    public async Task LoadAllConfigsAsync(string password, CancellationToken ct = default)
    {
        foreach (DataServiceType serviceType in Enum.GetValues<DataServiceType>())
        {
            await LoadConfigAsync(serviceType, password, ct);
        }
    }

    public async Task<IReadOnlyList<DataServiceConfig>> GetAllConfigsAsync(CancellationToken ct = default)
    {
        return await _storage.GetAllDataServiceConfigsAsync(ct);
    }
}
