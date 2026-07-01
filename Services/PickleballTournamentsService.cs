using Newtonsoft.Json;
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Models.PbTournamentsModels;

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
            throw new Exception($"PickleballTournaments API failed to {nameof(GetEventPlayersAsync)} due to status code {(int)response.StatusCode}: {body}");
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

    public async Task<TournamentEventsResponse> GetEventInfo(string eventName)
    {
        var url = $"https://pickleballtournaments.com/tournaments/api/tourneyEvents?slug={Uri.EscapeDataString(eventName)}";

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"PickleballTournaments API failed to {nameof(GetEventInfo)} due to status code {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();

        try
        {
            var deserializedResponse = NewtonsoftHttpJson.DeserializeString<TournamentEventsResponse>(json);

            if (deserializedResponse == null)
            {
                throw new JsonException($"Failed to deserialize {json} into {nameof(TournamentEventsResponse)}.");
            }

            return deserializedResponse;
        }
        catch (Exception e)
        {
            throw new JsonException($"Failed to deserilaize {json}.", e);
        }

    }
}


// Newtonsoft.Json is case-insensitive by default; no property attribute needed.
file class EventPlayersResponse
{
    public List<EventPlayer> Data { get; set; } = [];
}
