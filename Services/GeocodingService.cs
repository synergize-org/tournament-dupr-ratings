using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TournamentDuprRatings.Services;

public class GeocodingService(HttpClient httpClient, string apiKey)
{
    public async Task<(double Lat, double Lng)> GeocodeZipAsync(string zip)
    {
        var url = $"https://maps.googleapis.com/maps/api/geocode/json" +
                  $"?address={Uri.EscapeDataString(zip)}&key={Uri.EscapeDataString(apiKey)}";

        var response = await httpClient.GetFromJsonAsync<GeocodeResponse>(url)
            ?? throw new Exception("Null response from Geocoding API.");

        if (response.Status == "ZERO_RESULTS" || response.Results.Count == 0)
            throw new ZeroResultsException();

        if (response.Status != "OK")
            throw new Exception($"Geocoding API error: {response.Status}");

        var loc = response.Results[0].Geometry.Location;
        return (loc.Lat, loc.Lng);
    }
}

public class ZeroResultsException() : Exception("Geocoding returned no results for that zip code.");

// Response shape — file-scoped to avoid polluting namespace
file class GeocodeResponse
{
    [JsonPropertyName("status")]  public string Status { get; set; } = "";
    [JsonPropertyName("results")] public List<GeocodeResult> Results { get; set; } = [];
}

file class GeocodeResult
{
    [JsonPropertyName("geometry")] public GeocodeGeometry Geometry { get; set; } = new();
}

file class GeocodeGeometry
{
    [JsonPropertyName("location")] public GeocodeLocation Location { get; set; } = new();
}

file class GeocodeLocation
{
    [JsonPropertyName("lat")] public double Lat { get; set; }
    [JsonPropertyName("lng")] public double Lng { get; set; }
}
