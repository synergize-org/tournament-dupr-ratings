using System.Text.Json;
using System.Text.Json.Serialization;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class PickleballTournamentsService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<EventPlayer>> GetEventPlayersAsync(string activityId)
    {
        var url = $"https://pickleballtournaments.com/tournaments/api/eventPlayers" +
                  $"?activityId={Uri.EscapeDataString(activityId)}&activitySplitId=null";

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"PickleballTournaments API error {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();

        // Try direct array first; fall back to { "data": [...] } wrapper
        try
        {
            return JsonSerializer.Deserialize<List<EventPlayer>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            var wrapped = JsonSerializer.Deserialize<EventPlayersResponse>(json, JsonOptions);
            return wrapped?.Data ?? [];
        }
    }
}

file class EventPlayersResponse
{
    [JsonPropertyName("data")] public List<EventPlayer> Data { get; set; } = [];
}
