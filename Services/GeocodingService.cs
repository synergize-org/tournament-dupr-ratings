namespace TournamentDuprRatings.Services;

public class GeocodingService(HttpClient httpClient, string apiKey)
{

    private static Dictionary<string, (double Lat, double Lng)> _geoCodeCache = new();
    public async Task<(double Lat, double Lng)> GeocodeZipAsync(string zip)
    {
        if (_geoCodeCache.TryGetValue(zip, out var cached))
        {
            return cached;
        }

        var url = $"https://maps.googleapis.com/maps/api/geocode/json" +
                  $"?address={Uri.EscapeDataString(zip)}&key={Uri.EscapeDataString(apiKey)}";

        var httpResponse = await httpClient.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var response = await NewtonsoftHttpJson.ReadFromJsonAsync<GeocodeResponse>(httpResponse.Content)
            ?? throw new Exception("Null response from Geocoding API.");

        if (response.Status == "ZERO_RESULTS" || response.Results.Count == 0)
            throw new ZeroResultsException();

        if (response.Status != "OK")
            throw new Exception($"Geocoding API error: {response.Status}");

        var loc = response.Results.FirstOrDefault()?.Geometry.Location;
        return _geoCodeCache[zip] = (loc?.Lat ?? 0, loc?.Lng ?? 0);
    }
}

public class ZeroResultsException() : Exception("Geocoding returned no results for that zip code.");

file class GeocodeResponse
{
    public string Status { get; set; } = "";
    public List<GeocodeResult> Results { get; set; } = [];
}

file class GeocodeResult
{
    public GeocodeGeometry Geometry { get; set; } = new();
}

file class GeocodeGeometry
{
    public GeocodeLocation Location { get; set; } = new();
}

file class GeocodeLocation
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
