using System.Net.Http.Json;
using Buriza.Data.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buriza.Data.Services;

/// <summary>
/// Service for fetching token metadata from COMP or custom endpoint.
/// Checks session for custom endpoint first, falls back to appsettings default.
/// </summary>
public class TokenMetadataService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<TokenMetadataServiceOptions> options,
    IDataServiceSessionProvider? sessionProvider = null)
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes);

    private string BaseUrl => sessionProvider?.GetDataServiceEndpoint(DataServiceType.TokenMetadata)
        ?? options.Value.BaseUrl;

    public async Task<TokenMetadata?> GetTokenMetadataAsync(string subject, CancellationToken ct = default)
    {
        string cacheKey = $"token:{subject}";

        if (cache.TryGetValue(cacheKey, out TokenMetadata? cached))
            return cached;

        using HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/metadata/{subject}");
        AddApiKeyHeader(request);

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        TokenMetadata? result = await response.Content.ReadFromJsonAsync<TokenMetadata>(ct);
        if (result is not null)
            cache.Set(cacheKey, result, _cacheDuration);

        return result;
    }

    public async Task<BatchTokenMetadataResponse?> GetBatchTokenMetadataAsync(
        List<string> subjects,
        int? limit = null,
        string? searchText = null,
        string? policyId = null,
        CancellationToken ct = default)
    {
        var payload = new { subjects, limit, searchText, policyId };

        using HttpRequestMessage request = new(HttpMethod.Post, $"{BaseUrl}/metadata")
        {
            Content = JsonContent.Create(payload)
        };
        AddApiKeyHeader(request);

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<BatchTokenMetadataResponse>(ct);
    }

    public async Task<List<TokenMetadata>> GetWalletTokensAsync(List<string> subjects, CancellationToken ct = default)
    {
        List<TokenMetadata> results = [];
        List<string> uncached = [];

        foreach (string subject in subjects)
        {
            string cacheKey = $"token:{subject}";
            if (cache.TryGetValue(cacheKey, out TokenMetadata? cached) && cached is not null)
                results.Add(cached);
            else
                uncached.Add(subject);
        }

        if (uncached.Count > 0)
        {
            BatchTokenMetadataResponse? batch = await GetBatchTokenMetadataAsync(uncached, ct: ct);
            if (batch?.Data is not null)
            {
                foreach (TokenMetadata token in batch.Data)
                {
                    cache.Set($"token:{token.Subject}", token, _cacheDuration);
                    results.Add(token);
                }
            }
        }

        return results;
    }

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        string? apiKey = sessionProvider?.GetDataServiceApiKey(DataServiceType.TokenMetadata)
            ?? options.Value.ApiKey;

        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
    }
}

public class TokenMetadataServiceOptions
{
    public const string SectionName = "TokenMetadataService";

    public int CacheDurationMinutes { get; set; } = 60;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class TokenMetadata
{
    public string Subject { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Ticker { get; set; }
    public string? Logo { get; set; }
    public string? Description { get; set; }
    public int Decimals { get; set; }
    public long? Quantity { get; set; }
    public string? AssetName { get; set; }
    public string? TokenType { get; set; }
    public string? Policy { get; set; }
    public string? Url { get; set; }
    public bool HasOnChainData { get; set; }
    public bool HasRegistryData { get; set; }
}

public class BatchTokenMetadataResponse
{
    public int Total { get; set; }
    public List<TokenMetadata> Data { get; set; } = [];
}
