using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2016.Excel;
using System.Net;
using System.Net.Http.Headers;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class DuprService(HttpClient httpClient, string bearerToken)
{

    private static Dictionary<string, List<DuprPlayerHit>> _playerSearchCache = new Dictionary<string, List<DuprPlayerHit>>();
    private static Dictionary<string, DuprPlayerInfo> _playerInfoCache = new Dictionary<string, DuprPlayerInfo>();
    private Dictionary<string, DuprSearchByDuprIdResponse> _duprIdSearchCache = new Dictionary<string, DuprSearchByDuprIdResponse>();

    public async Task<List<DuprPlayerHit>> SearchAsync(
        string fullName, double lat, double lng)
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

    public async Task<DuprPlayerInfo> GetPlayerInfo(string duprId)
    {
        if (_playerInfoCache.ContainsKey(duprId))
        {
            return _playerInfoCache[duprId];
        }

        var playerIdResponse = await SearchByDuprId(duprId);
        var playerId = playerIdResponse.Results.FirstOrDefault()?.UserId;

        if (playerId == null || playerId == 0)
        {
            throw new Exception($"Unable to find player DUPR ID {duprId}");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.dupr.gg/player/v1.0/{playerId}");

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var httpResponse = await httpClient.SendAsync(httpRequest);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new DuprUnauthorizedException();

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new Exception($"DUPR API error {(int)httpResponse.StatusCode}: {body}");
        }

        var result = await NewtonsoftHttpJson.ReadFromJsonAsync<DuprPlayerInfo>(httpResponse.Content);
        return _playerInfoCache[duprId] = result ?? new DuprPlayerInfo();
    }

    public async Task<DuprSearchByDuprIdResponse> SearchByDuprId(string duprId)
    {
        if (_duprIdSearchCache.ContainsKey(duprId))
        {
            return _duprIdSearchCache[duprId];
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api.dupr.gg/player/search/byDuprId")
        {
            Content = NewtonsoftHttpJson.CreateJsonContent(new { DuprId = duprId })
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

        var result = await NewtonsoftHttpJson.ReadFromJsonAsync<DuprSearchByDuprIdResponse>(httpResponse.Content);
        return _duprIdSearchCache[duprId] = result ?? new DuprSearchByDuprIdResponse();
    }
}

public class DuprUnauthorizedException() : Exception("Invalid or expired Bearer Token.");
