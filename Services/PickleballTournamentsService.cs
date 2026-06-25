using Newtonsoft.Json;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class PickleballTournamentsService(HttpClient httpClient)
{
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
            return NewtonsoftHttpJson.DeserializeString<List<EventPlayer>>(json) ?? [];
        }
        catch (JsonException)
        {
            var wrapped = NewtonsoftHttpJson.DeserializeString<EventPlayersResponse>(json);
            return wrapped?.Data ?? [];
        }
    }
}

// Newtonsoft.Json is case-insensitive by default; no property attribute needed.
file class EventPlayersResponse
{
    public List<EventPlayer> Data { get; set; } = [];
}
