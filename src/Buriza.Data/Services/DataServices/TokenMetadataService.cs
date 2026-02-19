using System.Net.Http.Json;
using System.Text.Json;
using Buriza.Core.Interfaces.DataServices;
using Buriza.Core.Models.DataServices;
using Buriza.Core.Models.Enums;
using Buriza.Data.Models.DataServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buriza.Data.Services.DataServices;

public class TokenMetadataService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<TokenMetadataServiceOptions> options,
    IDataServiceSessionProvider? sessionProvider = null) : ITokenMetadataService
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes);

    private string BaseUrl => sessionProvider?.GetDataServiceEndpoint(DataServiceType.TokenMetadata)
        ?? options.Value.BaseUrl;

    public async Task<TokenMetadata?> GetTokenMetadataAsync(string assetId, CancellationToken ct = default)
    {
        string cacheKey = $"token:{assetId}";

        if (cache.TryGetValue(cacheKey, out TokenMetadata? cached))
            return cached;

        using HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/metadata/{assetId}");
        AddApiKeyHeader(request);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            TokenMetadataDto? dto = await response.Content.ReadFromJsonAsync<TokenMetadataDto>(ct);
            if (dto is null)
                return null;

            TokenMetadata result = MapToTokenMetadata(dto);
            cache.Set(cacheKey, result, _cacheDuration);
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<List<TokenMetadata>> GetTokenMetadataAsync(List<string> assetIds, CancellationToken ct = default)
    {
        List<TokenMetadata> results = [];
        List<string> uncached = [];

        foreach (string assetId in assetIds)
        {
            string cacheKey = $"token:{assetId}";
            if (cache.TryGetValue(cacheKey, out TokenMetadata? cached) && cached is not null)
                results.Add(cached);
            else
                uncached.Add(assetId);
        }

        if (uncached.Count > 0)
        {
            BatchTokenMetadataResponse? batch = await FetchBatchAsync(uncached, ct);
            if (batch?.Data is not null)
            {
                foreach (TokenMetadataDto dto in batch.Data)
                {
                    TokenMetadata token = MapToTokenMetadata(dto);
                    cache.Set($"token:{token.AssetId}", token, _cacheDuration);
                    results.Add(token);
                }
            }
        }

        return results;
    }

    private async Task<BatchTokenMetadataResponse?> FetchBatchAsync(List<string> subjects, CancellationToken ct)
    {
        var payload = new { subjects };

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

    private static TokenMetadata MapToTokenMetadata(TokenMetadataDto dto) => new()
    {
        AssetId = dto.Subject,
        Name = dto.Name,
        Ticker = dto.Ticker,
        Logo = dto.Logo,
        Description = dto.Description,
        Decimals = dto.Decimals
    };

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        string? apiKey = sessionProvider?.GetDataServiceApiKey(DataServiceType.TokenMetadata)
            ?? options.Value.ApiKey;

        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
    }
}
