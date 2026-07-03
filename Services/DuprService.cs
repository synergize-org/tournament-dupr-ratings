using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office2016.Excel;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class DuprService(HttpClient httpClient, string bearerToken)
{
    // Instance-level (not static) so cached data can never leak between DuprService instances
    // using different bearer tokens. Concurrent because player lookups now run in parallel.
    private readonly ConcurrentDictionary<string, DuprPlayerInfo> _playerInfoCache = new ConcurrentDictionary<string, DuprPlayerInfo>();
    private readonly ConcurrentDictionary<string, DuprSearchByDuprIdResponse> _duprIdSearchCache = new ConcurrentDictionary<string, DuprSearchByDuprIdResponse>();

    public async Task<DuprPlayerInfo> GetPlayerInfo(string duprId)
    {
        if (_playerInfoCache.TryGetValue(duprId, out var cachedInfo))
        {
            return cachedInfo;
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
        if (_duprIdSearchCache.TryGetValue(duprId, out var cachedResponse))
        {
            return cachedResponse;
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
