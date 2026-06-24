# tournament-dupr-ratings Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use executing-plans skill to implement this plan task-by-task.

**Goal:** Build a .NET 8 console app that cross-references pickleball tournament players with their DUPR ratings, outputting results to console and CSV.

**Architecture:** Structured service classes instantiated directly in `Program.cs` (no DI container). Each service owns one external API. Sequential DUPR lookups support interactive disambiguation prompts. A standalone `PlayerListBuilder` class holds pure filter logic for testability.

**Tech Stack:** .NET 8, `System.Net.Http.Json` (built-in), `System.Text.Json` (built-in), xUnit 2.x (tests)

---

### Task 1: Scaffold the project

**Files:**

- Create: `tournament-dupr-ratings.csproj` (via `dotnet new`)
- Create: `tournament-dupr-ratings.Tests/tournament-dupr-ratings.Tests.csproj`
- Create: `tournament-dupr-ratings.sln`

**Step 1: Scaffold console project in repo root**

Run from `c:\Users\von\source\repos\tournament-dupr-ratings`:

```
dotnet new console --framework net8.0 --force
```

(`--force` writes into the existing directory without touching `.git` or `.docs`)

**Step 2: Create test project**

```
dotnet new xunit -o tournament-dupr-ratings.Tests --framework net8.0
```

**Step 3: Create solution and link both projects**

```
dotnet new sln -n tournament-dupr-ratings
dotnet sln add tournament-dupr-ratings.csproj
dotnet sln add tournament-dupr-ratings.Tests/tournament-dupr-ratings.Tests.csproj
dotnet add tournament-dupr-ratings.Tests/tournament-dupr-ratings.Tests.csproj reference tournament-dupr-ratings.csproj
```

**Step 4: Create folder structure**

```
mkdir Models
mkdir Services
mkdir Output
```

**Step 5: Replace generated `tournament-dupr-ratings.csproj` content**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**Step 6: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 7: Commit**

```
git add -A
git commit -m "chore: scaffold .NET 8 console project with test project"
```

---

### Task 2: Define models

**Files:**

- Create: `Models/EventPlayer.cs`
- Create: `Models/DuprSearchRequest.cs`
- Create: `Models/DuprSearchResult.cs`
- Create: `Models/TeamResult.cs`

**Step 1: Create `Models/EventPlayer.cs`**

```csharp
namespace TournamentDuprRatings.Models;

public class EventPlayer
{
    public string? PlayerFullName { get; set; }
    public string? PartnerFullName { get; set; }
    public string? PlayerSlug { get; set; }
    public string? PartnerSlug { get; set; }
    public string? PlayerSkill { get; set; }
    public string? PartnerSkill { get; set; }
    public bool PlayerDuprActive { get; set; }
    public bool PartnerDuprActive { get; set; }
    public bool NeedAPartner { get; set; }
}
```

**Step 2: Create `Models/DuprSearchRequest.cs`**

```csharp
namespace TournamentDuprRatings.Models;

public class DuprSearchRequest
{
    public int Limit { get; set; } = 10;
    public int Offset { get; set; } = 0;
    public string Query { get; set; } = "";
    public string[] Exclude { get; set; } = [];
    public bool IncludeUnclaimedPlayers { get; set; } = true;
    public DuprSearchFilter Filter { get; set; } = new();
}

public class DuprSearchFilter
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DuprRatingFilter Rating { get; set; } = new();
    public string LocationText { get; set; } = "";
}

public class DuprRatingFilter
{
    public double? MaxRating { get; set; }
    public double? MinRating { get; set; }
}
```

**Step 3: Create `Models/DuprSearchResult.cs`**

> **Note:** Verify the actual DUPR API response shape by calling the endpoint with a real token before trusting this model. Adjust property names/nesting if the actual shape differs.

```csharp
namespace TournamentDuprRatings.Models;

public class DuprSearchResponse
{
    public DuprSearchResultWrapper? Result { get; set; }
}

public class DuprSearchResultWrapper
{
    public List<DuprPlayerHit> Hits { get; set; } = [];
}

public class DuprPlayerHit
{
    public string? Id { get; set; }
    public string? FullName { get; set; }
    public string? ShortAddress { get; set; }
    public int? Age { get; set; }
    public string? DuprId { get; set; }
    public DuprRatings? Ratings { get; set; }
}

public class DuprRatings
{
    public double? Doubles { get; set; }
    public bool DoublesVerified { get; set; }
    public double? Singles { get; set; }
    public bool SinglesVerified { get; set; }
}
```

**Step 4: Create `Models/TeamResult.cs`**

```csharp
namespace TournamentDuprRatings.Models;

public class TeamResult
{
    public string Player1Name { get; set; } = "";
    public string? Player1DuprId { get; set; }
    public string Player1Doubles { get; set; } = "";
    public string Player1Singles { get; set; } = "";

    public string Player2Name { get; set; } = "";
    public string? Player2DuprId { get; set; }
    public string Player2Doubles { get; set; } = "";
    public string Player2Singles { get; set; } = "";
}
```

**Step 5: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 6: Commit**

```
git add Models/
git commit -m "feat: add domain models"
```

---

### Task 3: PlayerListBuilder (pure logic, TDD)

Extracts the unique player entries from the `EventPlayer` list, applying the skip rules from the design.

**Files:**

- Create: `Services/PlayerListBuilder.cs`
- Create: `tournament-dupr-ratings.Tests/PlayerListBuilderTests.cs`

**Step 1: Write the failing tests**

Create `tournament-dupr-ratings.Tests/PlayerListBuilderTests.cs`:

```csharp
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Services;

namespace TournamentDuprRatings.Tests;

public class PlayerListBuilderTests
{
    [Fact]
    public void BuildUniquePlayerList_IncludesPlayerAndActivePartner()
    {
        var players = new List<EventPlayer>
        {
            new() { PlayerFullName = "Alice A", PlayerSlug = "alice", PartnerFullName = "Bob B", PartnerSlug = "bob", PartnerDuprActive = true }
        };

        var result = PlayerListBuilder.BuildUniquePlayerList(players);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.FullName == "Alice A");
        Assert.Contains(result, p => p.FullName == "Bob B");
    }

    [Fact]
    public void BuildUniquePlayerList_SkipsBlankPartnerName()
    {
        var players = new List<EventPlayer>
        {
            new() { PlayerFullName = "Alice A", PlayerSlug = "alice", PartnerFullName = "", PartnerDuprActive = true }
        };

        var result = PlayerListBuilder.BuildUniquePlayerList(players);

        Assert.Single(result);
        Assert.Equal("Alice A", result[0].FullName);
    }

    [Fact]
    public void BuildUniquePlayerList_SkipsInactivePartner()
    {
        var players = new List<EventPlayer>
        {
            new() { PlayerFullName = "Alice A", PlayerSlug = "alice", PartnerFullName = "Bob B", PartnerSlug = "bob", PartnerDuprActive = false }
        };

        var result = PlayerListBuilder.BuildUniquePlayerList(players);

        Assert.Single(result);
        Assert.Equal("Alice A", result[0].FullName);
    }

    [Fact]
    public void BuildUniquePlayerList_DeduplicatesAcrossTeams()
    {
        var players = new List<EventPlayer>
        {
            new() { PlayerFullName = "Alice A", PlayerSlug = "alice", PartnerFullName = "Bob B", PartnerSlug = "bob", PartnerDuprActive = true },
            new() { PlayerFullName = "Bob B",   PlayerSlug = "bob",   PartnerFullName = "Alice A", PartnerSlug = "alice", PartnerDuprActive = true }
        };

        var result = PlayerListBuilder.BuildUniquePlayerList(players);

        Assert.Equal(2, result.Count);
    }
}
```

**Step 2: Run tests to verify they fail**

```
dotnet test tournament-dupr-ratings.Tests
```

Expected: compile error — `PlayerListBuilder` not defined yet.

**Step 3: Create `Services/PlayerListBuilder.cs`**

```csharp
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services;

public record PlayerEntry(string FullName, string? Slug);

public static class PlayerListBuilder
{
    public static List<PlayerEntry> BuildUniquePlayerList(IEnumerable<EventPlayer> eventPlayers)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PlayerEntry>();

        foreach (var ep in eventPlayers)
        {
            AddIfNew(ep.PlayerFullName, ep.PlayerSlug, seen, result);

            if (!string.IsNullOrWhiteSpace(ep.PartnerFullName) && ep.PartnerDuprActive)
                AddIfNew(ep.PartnerFullName, ep.PartnerSlug, seen, result);
        }

        return result;
    }

    private static void AddIfNew(string? name, string? slug, HashSet<string> seen, List<PlayerEntry> result)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (seen.Add(name))
            result.Add(new PlayerEntry(name, slug));
    }
}
```

**Step 4: Run tests to verify they pass**

```
dotnet test tournament-dupr-ratings.Tests
```

Expected: 4 tests pass.

**Step 5: Commit**

```
git add Services/PlayerListBuilder.cs tournament-dupr-ratings.Tests/PlayerListBuilderTests.cs
git commit -m "feat: add PlayerListBuilder with tests"
```

---

### Task 4: GeocodingService

**Files:**

- Create: `Services/GeocodingService.cs`

**Step 1: Create `Services/GeocodingService.cs`**

```csharp
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
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Services/GeocodingService.cs
git commit -m "feat: add GeocodingService"
```

---

### Task 5: PickleballTournamentsService

**Files:**

- Create: `Services/PickleballTournamentsService.cs`

> **Note:** The exact JSON shape returned by the eventPlayers endpoint is unverified. Test with a real activity ID immediately after wiring `Program.cs`. If the response is wrapped (e.g. `{ "data": [...] }`), the fallback branch handles it; if it's a plain array, the first branch handles it.

**Step 1: Create `Services/PickleballTournamentsService.cs`**

```csharp
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
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Services/PickleballTournamentsService.cs
git commit -m "feat: add PickleballTournamentsService"
```

---

### Task 6: DuprService

**Files:**

- Create: `Services/DuprService.cs`

> **Note:** Verify the DUPR API response JSON shape when testing end-to-end. Adjust `DuprSearchResponse` in `Models/DuprSearchResult.cs` if the actual nesting differs.

**Step 1: Create `Services/DuprService.cs`**

```csharp
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
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Services/DuprService.cs
git commit -m "feat: add DuprService"
```

---

### Task 7: ConsoleOutput

**Files:**

- Create: `Output/ConsoleOutput.cs`

**Step 1: Create `Output/ConsoleOutput.cs`**

```csharp
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Output;

public static class ConsoleOutput
{
    public static void PrintTable(List<TeamResult> teams)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{"#",-4} {"Player 1",-26} {"P1 DUPR ID",-12} {"P1 Dbl",-8} {"P1 Sgl",-8} " +
            $"{"Player 2",-26} {"P2 DUPR ID",-12} {"P2 Dbl",-8} {"P2 Sgl"}");
        Console.WriteLine(new string('-', 122));

        for (int i = 0; i < teams.Count; i++)
        {
            var t = teams[i];
            Console.WriteLine(
                $"{i + 1,-4} {t.Player1Name,-26} {t.Player1DuprId ?? "",-12} {t.Player1Doubles,-8} {t.Player1Singles,-8} " +
                $"{t.Player2Name,-26} {t.Player2DuprId ?? "",-12} {t.Player2Doubles,-8} {t.Player2Singles}");
        }

        Console.WriteLine();
    }
}
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Output/ConsoleOutput.cs
git commit -m "feat: add ConsoleOutput"
```

---

### Task 8: CsvOutput

**Files:**

- Create: `Output/CsvOutput.cs`

**Step 1: Create `Output/CsvOutput.cs`**

```csharp
using System.Text;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Output;

public static class CsvOutput
{
    public static void WriteFile(List<TeamResult> teams, string activityId)
    {
        var filename = $"tournament-{activityId}-ratings.csv";
        var sb = new StringBuilder();

        sb.AppendLine("Team,Player1,Player1 DUPR ID,Player1 Doubles,Player1 Singles," +
                      "Player2,Player2 DUPR ID,Player2 Doubles,Player2 Singles");

        for (int i = 0; i < teams.Count; i++)
        {
            var t = teams[i];
            sb.AppendLine(string.Join(',',
            [
                (i + 1).ToString(),
                Escape(t.Player1Name), Escape(t.Player1DuprId), Escape(t.Player1Doubles), Escape(t.Player1Singles),
                Escape(t.Player2Name), Escape(t.Player2DuprId), Escape(t.Player2Doubles), Escape(t.Player2Singles)
            ]));
        }

        File.WriteAllText(filename, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Results saved to: {Path.GetFullPath(filename)}");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Output/CsvOutput.cs
git commit -m "feat: add CsvOutput"
```

---

### Task 9: Program.cs — orchestration

**Files:**

- Modify: `Program.cs`

**Step 1: Replace generated `Program.cs` with the full orchestration**

```csharp
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Output;
using TournamentDuprRatings.Services;

// Guard: GOOGLE_API_KEY must be set before prompting
var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
if (string.IsNullOrWhiteSpace(googleApiKey))
{
    Console.Error.WriteLine("Error: GOOGLE_API_KEY environment variable is not set.");
    return 1;
}

// Collect inputs
Console.Write("DUPR Bearer Token: ");
var bearerToken = ReadMaskedInput();

Console.Write("Activity ID: ");
var activityId = Console.ReadLine()?.Trim() ?? "";

var httpClient = new HttpClient();
var geocodingService = new GeocodingService(httpClient, googleApiKey);

double lat = 0, lng = 0;
while (true)
{
    Console.Write("Tournament zip code: ");
    var zip = Console.ReadLine()?.Trim() ?? "";
    try
    {
        (lat, lng) = await geocodingService.GeocodeZipAsync(zip);
        break;
    }
    catch (ZeroResultsException)
    {
        Console.WriteLine("No geocoding results for that zip code. Please try again.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Geocoding error: {ex.Message}");
        return 1;
    }
}

// Fetch event players
Console.WriteLine("Fetching tournament players...");
var tournamentService = new PickleballTournamentsService(httpClient);
List<EventPlayer> eventPlayers;
try
{
    eventPlayers = await tournamentService.GetEventPlayersAsync(activityId);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error fetching players: {ex.Message}");
    return 1;
}

if (eventPlayers.Count == 0)
{
    Console.Error.WriteLine("No players found for this activity ID.");
    return 1;
}
Console.WriteLine($"Found {eventPlayers.Count} team entries.");

// Build unique player list
var uniquePlayers = PlayerListBuilder.BuildUniquePlayerList(eventPlayers);
Console.WriteLine($"Unique players to look up: {uniquePlayers.Count}");

// DUPR lookups — sequential so disambiguation prompts never interleave
var duprService = new DuprService(httpClient);
var playerResults = new Dictionary<string, DuprPlayerHit?>(StringComparer.OrdinalIgnoreCase);
var skippedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var player in uniquePlayers)
{
    Console.Write($"Looking up: {player.FullName}... ");

    List<DuprPlayerHit> hits;
    try
    {
        hits = await duprService.SearchAsync(player.FullName, lat, lng, bearerToken);
    }
    catch (DuprUnauthorizedException)
    {
        Console.Error.WriteLine("\nError: Invalid or expired Bearer Token.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nDUPR API error: {ex.Message}");
        return 1;
    }

    if (hits.Count == 0)
    {
        Console.WriteLine("Not Found");
        playerResults[player.FullName] = null;
    }
    else if (hits.Count == 1)
    {
        Console.WriteLine($"Found: {hits[0].FullName}");
        playerResults[player.FullName] = hits[0];
    }
    else
    {
        Console.WriteLine($"{hits.Count} matches — please select:");
        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            Console.WriteLine($"  {i + 1}. {h.FullName} | {h.ShortAddress} | Age: {h.Age} " +
                              $"| Dbl: {FormatRating(h.Ratings?.Doubles)} | Sgl: {FormatRating(h.Ratings?.Singles)}");
        }
        Console.Write("Enter number to select, or 0 to skip: ");
        var choice = Console.ReadLine()?.Trim();

        if (int.TryParse(choice, out var idx) && idx >= 1 && idx <= hits.Count)
        {
            playerResults[player.FullName] = hits[idx - 1];
        }
        else
        {
            Console.WriteLine("Skipped.");
            skippedPlayers.Add(player.FullName);
            playerResults[player.FullName] = null;
        }
    }
}

// Assemble TeamResults
var teams = new List<TeamResult>();
foreach (var ep in eventPlayers)
{
    var team = new TeamResult
    {
        Player1Name    = ep.PlayerFullName ?? "",
        Player1Doubles = ResolveRatingDisplay(ep.PlayerFullName, "Doubles", playerResults, skippedPlayers),
        Player1Singles = ResolveRatingDisplay(ep.PlayerFullName, "Singles", playerResults, skippedPlayers),
        Player1DuprId  = ResolveHit(ep.PlayerFullName, playerResults)?.DuprId
    };

    if (string.IsNullOrWhiteSpace(ep.PartnerFullName) || !ep.PartnerDuprActive)
    {
        team.Player2Name    = "N/A";
        team.Player2Doubles = "N/A";
        team.Player2Singles = "N/A";
    }
    else
    {
        team.Player2Name    = ep.PartnerFullName;
        team.Player2Doubles = ResolveRatingDisplay(ep.PartnerFullName, "Doubles", playerResults, skippedPlayers);
        team.Player2Singles = ResolveRatingDisplay(ep.PartnerFullName, "Singles", playerResults, skippedPlayers);
        team.Player2DuprId  = ResolveHit(ep.PartnerFullName, playerResults)?.DuprId;
    }

    teams.Add(team);
}

// Output
ConsoleOutput.PrintTable(teams);
CsvOutput.WriteFile(teams, activityId);

return 0;

// ── Helpers ────────────────────────────────────────────────────────────────

static string ReadMaskedInput()
{
    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(intercept: true);
        if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
        {
            sb.Append(key.KeyChar);
            Console.Write("*");
        }
        else if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
        {
            sb.Remove(sb.Length - 1, 1);
            Console.Write("\b \b");
        }
    } while (key.Key != ConsoleKey.Enter);
    Console.WriteLine();
    return sb.ToString();
}

static string FormatRating(double? rating) =>
    rating.HasValue ? rating.Value.ToString("F2") : "NR";

static DuprPlayerHit? ResolveHit(
    string? name,
    Dictionary<string, DuprPlayerHit?> results)
{
    if (string.IsNullOrWhiteSpace(name)) return null;
    return results.TryGetValue(name, out var hit) ? hit : null;
}

static string ResolveRatingDisplay(
    string? name,
    string ratingType,
    Dictionary<string, DuprPlayerHit?> results,
    HashSet<string> skipped)
{
    if (string.IsNullOrWhiteSpace(name)) return "Not Found";
    if (skipped.Contains(name)) return "Skipped";
    if (!results.TryGetValue(name, out var hit) || hit == null) return "Not Found";

    return ratingType == "Doubles"
        ? FormatRating(hit.Ratings?.Doubles)
        : FormatRating(hit.Ratings?.Singles);
}
```

**Step 2: Verify build**

```
dotnet build
```

Expected: `Build succeeded.`

**Step 3: Commit**

```
git add Program.cs
git commit -m "feat: wire Program.cs orchestration"
```

---

### Task 10: Final verification

**Step 1: Run all tests**

```
dotnet test
```

Expected: all 4 unit tests pass.

**Step 2: Build in Release mode**

```
dotnet build -c Release
```

Expected: `Build succeeded.`

**Step 3: Smoke test end-to-end**

Set `GOOGLE_API_KEY` to a real key, then run:

```
dotnet run
```

Enter a real activity ID from pickleballtournaments.com. Verify:
- Zip geocodes correctly
- Players are fetched and listed
- DUPR lookups proceed sequentially
- Disambiguation prompt appears when multiple hits are returned
- Console table and CSV file are produced

If the `PickleballTournaments` or `DuprSearchResponse` models don't match the actual API responses, adjust the model properties and try again before the final commit.

**Step 4: Final commit**

```
git add -A
git commit -m "chore: final build and smoke test verification"
```
