using System.Net;
using System.Net.Http.Headers;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class DuprService(HttpClient httpClient)
{

    private static Dictionary<string, List<DuprPlayerHit>> _playerSearchCache = new Dictionary<string, List<DuprPlayerHit>>();

    public async Task<List<DuprPlayerHit>> SearchAsync(
        string fullName, double lat, double lng, string bearerToken)
    {
        if (_playerSearchCache.ContainsKey(fullName))
        {
            return _playerSearchCache[fullName];
        }

        var request = new DuprSearchRequest
        {
            Query = fullName,
            Filter = new DuprSearchFilter { Lat = lat, Lng = lng }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dupr.gg/player/v1.0/search")
        {
            Content = NewtonsoftHttpJson.CreateJsonContent(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var httpResponse = await httpClient.SendAsync(httpRequest);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new DuprUnauthorizedException();

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new Exception($"DUPR API error {(int)httpResponse.StatusCode}: {body}");
        }

        var result = await NewtonsoftHttpJson.ReadFromJsonAsync<DuprSearchResponse>(httpResponse.Content);
        return _playerSearchCache[fullName] = result?.Result?.Hits ?? [];
    }
}

public class DuprUnauthorizedException() : Exception("Invalid or expired Bearer Token.");
