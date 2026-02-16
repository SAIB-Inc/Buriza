using System.Net.Http.Json;
using System.Text.Json;
using Buriza.Core.Interfaces.DataServices;
using Buriza.Core.Models.DataServices;
using Buriza.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buriza.Data.Services.DataServices;

/// <summary>
/// Service for fetching token metadata from COMP or custom endpoint.
/// </summary>
public class TokenMetadataService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<TokenMetadataServiceOptions> options,
    IDataServiceSessionProvider? sessionProvider = null) : ITokenMetadataService
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

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            TokenMetadata? result = await response.Content.ReadFromJsonAsync<TokenMetadata>(ct);
            if (result is not null)
                cache.Set(cacheKey, result, _cacheDuration);

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
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

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<BatchTokenMetadataResponse>(ct);
        }
        catch (JsonException)
        {
            return null;
        }
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
