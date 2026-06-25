using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TournamentDuprRatings.Services;

internal static class NewtonsoftHttpJson
{
    private static readonly JsonSerializerSettings CamelCaseSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    // Newtonsoft.Json is case-insensitive by default; no extra settings needed for deserialization.

    /// <summary>Creates a camelCase JSON request body from <paramref name="value"/>.</summary>
    public static StringContent CreateJsonContent<T>(T value)
    {
        var json = JsonConvert.SerializeObject(value, CamelCaseSettings);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>Deserializes the response body of <paramref name="content"/> to <typeparamref name="T"/>.</summary>
    public static async Task<T?> ReadFromJsonAsync<T>(HttpContent content)
    {
        var json = await content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    /// <summary>Deserializes a raw JSON string to <typeparamref name="T"/> (for fallback scenarios).</summary>
    public static T? DeserializeString<T>(string json)
        => JsonConvert.DeserializeObject<T>(json);
}
