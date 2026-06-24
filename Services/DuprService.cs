using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public class DuprService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<DuprPlayerHit>> SearchAsync(
        string fullName, double lat, double lng, string bearerToken)
    {
        var request = new DuprSearchRequest
        {
            Query = fullName,
            Filter = new DuprSearchFilter { Lat = lat, Lng = lng }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dupr.gg/player/v1.0/search")
        {
            Content = JsonContent.Create(request, options: CamelCase)
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

        var result = await httpResponse.Content.ReadFromJsonAsync<DuprSearchResponse>(CaseInsensitive);
        return result?.Result?.Hits ?? [];
    }
}

public class DuprUnauthorizedException() : Exception("Invalid or expired Bearer Token.");
