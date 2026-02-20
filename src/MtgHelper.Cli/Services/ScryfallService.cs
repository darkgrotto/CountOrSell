using System.Text.Json;
using MtgHelper.Core.Models;

namespace MtgHelper.Cli.Services;

public class ScryfallService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ScryfallService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MtgSet>> GetSetsAsync()
    {
        var response = await _httpClient.GetAsync("sets");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ScryfallSetListResponse>(content, _jsonOptions);
        return result?.Data ?? new List<MtgSet>();
    }

    public async Task<List<MtgCard>> GetCardsAsync(string setCode)
    {
        var allCards = new List<MtgCard>();
        var nextPageUrl = $"cards/search?order=set&q=set:{setCode.ToLowerInvariant()}&unique=prints";

        while (!string.IsNullOrEmpty(nextPageUrl))
        {
            await Task.Delay(100);
            var response = await _httpClient.GetAsync(nextPageUrl);
            if (!response.IsSuccessStatusCode) break;

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ScryfallCardListResponse>(content, _jsonOptions);

            if (result?.Data != null)
                allCards.AddRange(result.Data);

            nextPageUrl = result?.HasMore == true ? result.NextPage : null;
            if (nextPageUrl != null && nextPageUrl.StartsWith("https://api.scryfall.com/"))
                nextPageUrl = nextPageUrl.Replace("https://api.scryfall.com/", "");
        }

        return allCards
            .OrderBy(c => int.TryParse(c.CollectorNumber.TrimEnd('a', 'b', 's', 'p'), out var num) ? num : 9999)
            .ThenBy(c => c.CollectorNumber)
            .ToList();
    }

    public async Task<List<MtgCard>> GetReserveListCardsAsync()
    {
        var allCards = new List<MtgCard>();
        var nextPageUrl = "cards/search?q=is%3Areserved&unique=prints&order=name";

        while (!string.IsNullOrEmpty(nextPageUrl))
        {
            await Task.Delay(100);
            var response = await _httpClient.GetAsync(nextPageUrl);
            if (!response.IsSuccessStatusCode) break;

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ScryfallCardListResponse>(content, _jsonOptions);

            if (result?.Data != null)
                allCards.AddRange(result.Data);

            nextPageUrl = result?.HasMore == true ? result.NextPage : null;
            if (nextPageUrl != null && nextPageUrl.StartsWith("https://api.scryfall.com/"))
                nextPageUrl = nextPageUrl.Replace("https://api.scryfall.com/", "");
        }

        return allCards;
    }

    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        try
        {
            return await _httpClient.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }
}
