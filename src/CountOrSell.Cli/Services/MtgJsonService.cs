using System.Text.Json;
using CountOrSell.Core.Models;

namespace CountOrSell.Cli.Services;

public class MtgJsonService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MtgJsonService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MtgJsonBoosterType>> GetBoosterTypesForSetAsync(string setCode)
    {
        var setList = await GetSetListAsync();
        var set = setList.FirstOrDefault(s =>
            s.Code.Equals(setCode, StringComparison.OrdinalIgnoreCase));

        if (set == null) return new List<MtgJsonBoosterType>();

        return set.SealedProduct
            .Where(p => p.Category.Equals("booster_pack", StringComparison.OrdinalIgnoreCase))
            .Select(p => new MtgJsonBoosterType { Name = p.Name, Subtype = p.Subtype })
            .ToList();
    }

    private async Task<List<MtgJsonSetListEntry>> GetSetListAsync()
    {
        var response = await _httpClient.GetAsync("SetList.json");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<MtgJsonSetListResponse>(content, _jsonOptions);
        return result?.Data ?? new List<MtgJsonSetListEntry>();
    }
}
