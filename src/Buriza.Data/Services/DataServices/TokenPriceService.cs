using System.Net.Http.Json;
using System.Text.Json;
using Buriza.Core.Interfaces.DataServices;
using Buriza.Core.Models.DataServices;
using Buriza.Core.Models.Enums;
using Buriza.Data.Models.DataServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Buriza.Data.Services.DataServices;

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

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>(ct);
        }
        catch (JsonException)
        {
            return null;
        }
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
        CancellationToken ct = default)
    {
        string cacheKey = $"pricechange:{unit}";

        if (cache.TryGetValue(cacheKey, out TokenPriceChange? cached))
            return cached;

        string url = $"{BaseUrl}/token/prices/chg?unit={Uri.EscapeDataString(unit)}";

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        AddApiKeyHeader(request);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            TokenPriceChangeDto? dto = await response.Content.ReadFromJsonAsync<TokenPriceChangeDto>(ct);
            if (dto is null)
                return null;

            TokenPriceChange result = MapToPriceChange(dto);
            cache.Set(cacheKey, result, _cacheDuration);
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TokenPriceChange MapToPriceChange(TokenPriceChangeDto dto)
    {
        TokenPriceChange result = new();

        if (dto.Chg1h.HasValue) result.Changes[PriceTimeframe.OneHour] = dto.Chg1h.Value;
        if (dto.Chg24h.HasValue) result.Changes[PriceTimeframe.TwentyFourHours] = dto.Chg24h.Value;
        if (dto.Chg7d.HasValue) result.Changes[PriceTimeframe.SevenDays] = dto.Chg7d.Value;
        if (dto.Chg30d.HasValue) result.Changes[PriceTimeframe.ThirtyDays] = dto.Chg30d.Value;

        return result;
    }

    public async Task<decimal?> GetNativeCoinPriceAsync(string quote = "USD", CancellationToken ct = default)
    {
        string cacheKey = $"nativeprice:{quote}";

        if (cache.TryGetValue(cacheKey, out decimal cachedPrice))
            return cachedPrice;

        using HttpRequestMessage request = new(HttpMethod.Get, $"{BaseUrl}/token/quote?quote={quote}");
        AddApiKeyHeader(request);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            QuoteResponse? result = await response.Content.ReadFromJsonAsync<QuoteResponse>(ct);
            if (result is null)
                return null;

            cache.Set(cacheKey, result.Price, _cacheDuration);
            return result.Price;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void AddApiKeyHeader(HttpRequestMessage request)
    {
        string? apiKey = sessionProvider?.GetDataServiceApiKey(DataServiceType.TokenPrice)
            ?? options.Value.ApiKey;

        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
    }
}
