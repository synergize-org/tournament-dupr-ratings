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

// Collect inputs — skip prompts for any values supplied as launch arguments
var bearerToken = GetArg(args, "bearer-token");
if (string.IsNullOrWhiteSpace(bearerToken))
{
    Console.Write("DUPR Bearer Token: ");
    bearerToken = ReadMaskedInput();
}

var activityId = GetArg(args, "activity-id");
if (string.IsNullOrWhiteSpace(activityId))
{
    Console.Write("Activity ID: ");
    activityId = Console.ReadLine()?.Trim() ?? "";
}

var httpClient = new HttpClient();
var geocodingService = new GeocodingService(httpClient, googleApiKey);

double lat = 0, lng = 0;
var zipArg = GetArg(args, "zip");
if (!string.IsNullOrWhiteSpace(zipArg))
{
    try
    {
        (lat, lng) = await geocodingService.GeocodeZipAsync(zipArg);
    }
    catch (ZeroResultsException)
    {
        Console.Error.WriteLine($"No geocoding results for zip code '{zipArg}'.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Geocoding error: {ex.Message}");
        return 1;
    }
}
else
{
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
        team.Player2DuprId  = "N/A";
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

static string? GetArg(string[] args, string name)
{
    var flag = $"--{name}";
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

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
