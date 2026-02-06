using System.Net.Http.Json;
using Buriza.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buriza.Data.Services.DataServices;

public interface ITokenPriceService
{
    Task<Dictionary<string, decimal>?> GetTokenPricesAsync(List<string> units, CancellationToken ct = default);
    Task<decimal?> GetTokenPriceAsync(string unit, CancellationToken ct = default);
    Task<TokenPriceChange?> GetPriceChangeAsync(string unit, string? timeframes = null, CancellationToken ct = default);
    Task<decimal?> GetAdaPriceAsync(string quote = "USD", CancellationToken ct = default);
}

/// <summary>
/// Service for fetching token prices from TapTools or custom endpoint.
/// </summary>
public class TokenPriceService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<TokenPriceServiceOptions> options,
    IDataServiceSessionProvider? sessionProvider = null) : ITokenPriceService
{
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes);

    private string BaseUrl => sessionProvider?.GetDataServiceEndpoint(DataServiceType.TokenPrice)
        ?? options.Value.BaseUrl;

    public async Task<Dictionary<string, decimal>?> GetTokenPricesAsync(List<string> units, CancellationToken ct = default)
    {
        if (units.Count == 0)
            return [];

        if (units.Count > 100)
            throw new ArgumentException("Maximum 100 tokens per request", nameof(units));

        using HttpRequestMessage request = new(HttpMethod.Post, $"{BaseUrl}/token/prices")
        {
            Content = JsonContent.Create(units)
        };
        AddApiKeyHeader(request);

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>(ct);
    }

    public async Task<decimal?> GetTokenPriceAsync(string unit, CancellationToken ct = default)
    {
        string cacheKey = $"price:{unit}";

        if (cache.TryGetValue(cacheKey, out decimal cachedPrice))
            return cachedPrice;

        Dictionary<string, decimal>? prices = await GetTokenPricesAsync([unit], ct);
        if (prices is null || !prices.TryGetValue(unit, out decimal price))
            return null;

        cache.Set(cacheKey, price, _cacheDuration);
        return price;
    }

    public async Task<TokenPriceChange?> GetPriceChangeAsync(
        string unit,
        string? timeframes = null,
        CancellationToken ct = default)
    {
        string cacheKey = $"pricechange:{unit}:{timeframes ?? "all"}";

        if (cache.TryGetValue(cacheKey, out TokenPriceChange? cached))
            return cached;

        string url = $"{BaseUrl}/token/prices/chg?unit={Uri.EscapeDataString(unit)}";
        if (!string.IsNullOrEmpty(timeframes))
            url += $"&timeframes={Uri.EscapeDataString(timeframes)}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        AddApiKeyHeader(request);

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        TokenPriceChange? result = await response.Content.ReadFromJsonAsync<TokenPriceChange>(ct);
        if (result is not null)
            cache.Set(cacheKey, result, _cacheDuration);

        return result;
    }

    public async Task<decimal?> GetAdaPriceAsync(string quote = "USD", CancellationToken ct = default)
    {
        string cacheKey = $"adaprice:{quote}";

        if (cache.TryGetValue(cacheKey, out decimal cachedPrice))
            return cachedPrice;

        using HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/token/quote?quote={quote}");
        AddApiKeyHeader(request);

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        QuoteResponse? result = await response.Content.ReadFromJsonAsync<QuoteResponse>(ct);
        if (result is null)
            return null;

        cache.Set(cacheKey, result.Price, _cacheDuration);
        return result.Price;
    }

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        string? apiKey = sessionProvider?.GetDataServiceApiKey(DataServiceType.TokenPrice)
            ?? options.Value.ApiKey;

        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
    }
}

public class TokenPriceServiceOptions
{
    public const string SectionName = "TokenPriceService";

    public int CacheDurationMinutes { get; set; } = 1;
    public string BaseUrl { get; set; } = "https://openapi.taptools.io";
    public string ApiKey { get; set; } = string.Empty;
}

public class TokenPriceChange
{
    public decimal? Chg5m { get; set; }
    public decimal? Chg1h { get; set; }
    public decimal? Chg4h { get; set; }
    public decimal? Chg6h { get; set; }
    public decimal? Chg24h { get; set; }
    public decimal? Chg7d { get; set; }
    public decimal? Chg30d { get; set; }
    public decimal? Chg60d { get; set; }
    public decimal? Chg90d { get; set; }
}

internal class QuoteResponse
{
    public decimal Price { get; set; }
}
