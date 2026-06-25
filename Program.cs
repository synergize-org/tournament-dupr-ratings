using TournamentDuprRatings.Models;
using TournamentDuprRatings.Models.PbTournamentsModels;
using TournamentDuprRatings.Output;
using TournamentDuprRatings.Services;

namespace TournamentDuprRatings;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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

        var tournamentName = GetArg(args, "tournament-name");
        if (string.IsNullOrEmpty(tournamentName)) { 
            Console.Write("Tournament name (slug): ");
            tournamentName = Console.ReadLine()?.Trim() ?? "";
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

        var tournamentService = new PickleballTournamentsService(httpClient);
        var fullTournamentDetails = await tournamentService.GetEventInfo(tournamentName);
        var tournaments = new Dictionary<string, List<EventPlayer>>();
        var tournamentInfo = new Dictionary<string, TournamentEvent>();
        var outPutInfo = new List<EventResults>();
        var cachedDuprId = new Dictionary<string, DuprPlayerHit?>();
        var lowerBoundary = new Dictionary<string, List<DuprPlayerHit>>();
        var upperBoundary = new Dictionary<string, List<DuprPlayerHit>>();

        foreach (var group in fullTournamentDetails.Events)
        {
            foreach (var bracket in group.Events)
            {
                tournamentInfo[bracket.ActivityId] = bracket;
                var skillGroup = GetSkillGroup(bracket.SkillGroup);

                // Fetch event players
                Console.WriteLine("Fetching tournament players...");

                try
                {
                    tournaments[bracket.ActivityId] = await tournamentService.GetEventPlayersAsync(bracket.ActivityId);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error fetching players: {ex.Message}");
                    return 1;
                }

                if (tournaments[bracket.ActivityId].Count == 0)
                {
                    Console.Error.WriteLine($"No players found for {bracket.Title}");
                    continue;
                }
                Console.WriteLine($"Found {tournaments[bracket.ActivityId].Count} team entries.");

                // Build unique player list
                var uniquePlayers = PlayerListBuilder.BuildUniquePlayerList(tournaments[bracket.ActivityId]);
                Console.WriteLine($"Unique players to look up: {uniquePlayers.Count}");

                // DUPR lookups — sequential so disambiguation prompts never interleave
                var duprService = new DuprService(httpClient);
                var playerResults = new Dictionary<string, DuprPlayerHit?>(StringComparer.OrdinalIgnoreCase);
                var skippedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var player in uniquePlayers)
                {
                    Console.Write($"Looking up: https://pickleball.com/players/{player.Slug} ");

                    var htmlScraper = new PickleballPlayerScraper();
                    var playerProfile = await htmlScraper.GetPlayerProfileAsync(player.Slug);                    

                    List<DuprPlayerHit> hits = new List<DuprPlayerHit>();
                    DuprPlayerHit duprPlayerHit = new DuprPlayerHit();
              
                    try
                    {
                        if (cachedDuprId.ContainsKey(playerProfile.DuprId))
                        {
                            playerResults[player.FullName] = cachedDuprId[player.Slug];
                        }
                        else
                        {
                            hits = await duprService.SearchAsync(playerProfile.DuprId, lat, lng, bearerToken);
                        }                        
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

                    if (hits.Count == 1)
                    {
                        Console.WriteLine($"Found: {hits.FirstOrDefault()?.FullName}");
                        var playerDupr = playerResults[player.FullName] = hits.FirstOrDefault();

                        if (skillGroup.lower != double.NaN && skillGroup.upper != double.NaN)
                        {
                            switch (bracket.Format)
                            {
                                case "Doubles":
                                    var doublesRating = double.TryParse(playerDupr?.Ratings?.Doubles, out var dblRating) ? dblRating : (double?)null;
                                    if (doublesRating > skillGroup.upper)
                                    {
                                        if (!upperBoundary.ContainsKey(bracket.Title))
                                        {
                                            upperBoundary[bracket.Title] = new List<DuprPlayerHit>();
                                        }

                                        upperBoundary[bracket.Title].Add(playerDupr);
                                    }

                                    if (doublesRating < skillGroup.lower)
                                    {
                                        if (!lowerBoundary.ContainsKey(bracket.Title))
                                        {
                                            lowerBoundary[bracket.Title] = new List<DuprPlayerHit>();
                                        }

                                        lowerBoundary[bracket.Title].Add(playerDupr);
                                    }
                                    break;
                                case "Singles":
                                    var singlesRating = double.TryParse(playerDupr?.Ratings?.Singles, out var sglRating) ? sglRating : (double?)null;
                                    if (singlesRating > skillGroup.upper)
                                    {
                                        if (!upperBoundary.ContainsKey(bracket.Title))
                                        {
                                            upperBoundary[bracket.Title] = new List<DuprPlayerHit>();
                                        }

                                        upperBoundary[bracket.Title].Add(playerDupr);
                                    }

                                    if (singlesRating < skillGroup.lower)
                                    {
                                        if (!lowerBoundary.ContainsKey(bracket.Title))
                                        {
                                            lowerBoundary[bracket.Title] = new List<DuprPlayerHit>();
                                        }

                                        lowerBoundary[bracket.Title].Add(playerDupr);
                                    }
                                    break;
                            }
                        }                        
                    }
                }

                // Assemble TeamResults
                var teams = new List<TeamResult>();
                foreach (var ep in tournaments[bracket.ActivityId])
                {
                    var team = new TeamResult
                    {
                        Player1Name = ep.PlayerFullName ?? "",
                        Player1Doubles = ResolveRatingDisplay(ep.PlayerFullName, "Doubles", playerResults, skippedPlayers),
                        Player1Singles = ResolveRatingDisplay(ep.PlayerFullName, "Singles", playerResults, skippedPlayers),
                        Player1DuprId = ResolveHit(ep.PlayerFullName, playerResults)?.DuprId
                    };

                    if (string.IsNullOrWhiteSpace(ep.PartnerFullName) || !ep.PartnerDuprActive)
                    {
                        team.Player2Name = "N/A";
                        team.Player2DuprId = "N/A";
                        team.Player2Doubles = "N/A";
                        team.Player2Singles = "N/A";
                    }
                    else
                    {
                        team.Player2Name = ep.PartnerFullName;
                        team.Player2Doubles = ResolveRatingDisplay(ep.PartnerFullName, "Doubles", playerResults, skippedPlayers);
                        team.Player2Singles = ResolveRatingDisplay(ep.PartnerFullName, "Singles", playerResults, skippedPlayers);
                        team.Player2DuprId = ResolveHit(ep.PartnerFullName, playerResults)?.DuprId;
                    }

                    teams.Add(team);
                }

                outPutInfo.Add(new EventResults
                {
                    SkillGroup = bracket.SkillGroup,
                    Title = bracket.Title,
                    TeamResults = teams
                });
            }
        }

        // Output
        ConsoleOutput.PrintTable(outPutInfo);

        if (lowerBoundary.Count > 0)
        {
            foreach (var kvp in lowerBoundary)
            {
                ConsoleOutput.PrintOutOfBoundsTableLower(kvp.Key, kvp.Value);
            }
        }

        if (upperBoundary.Count > 0)
        {
            foreach (var kvp in upperBoundary)
            {
                ConsoleOutput.PrintOutOfBoundsTableUpper(kvp.Key, kvp.Value);
            }
        }

        return 0;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string? GetArg(string[] args, string name)
    {
        var flag = $"--{name}";
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static string ReadMaskedInput()
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

    private static DuprPlayerHit? ResolveHit(
        string? name,
        Dictionary<string, DuprPlayerHit?> results)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return results.TryGetValue(name, out var hit) ? hit : null;
    }

    private static string ResolveRatingDisplay(
        string? name,
        string ratingType,
        Dictionary<string, DuprPlayerHit?> results,
        HashSet<string> skipped)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Not Found";
        if (skipped.Contains(name)) return "Skipped";
        if (!results.TryGetValue(name, out var hit) || hit == null) return "Not Found";

        return ratingType == "Doubles"
            ? hit.Ratings?.Doubles ?? ""
            : hit.Ratings?.Singles ?? "";
    }

    private static (double lower, double upper) GetSkillGroup(string skillGroup)
    {
        var skillGroupLower = skillGroup.ToLower();
        if (skillGroupLower.Contains("to"))
        {
            var split = skillGroupLower.Split("to");
            return (double.Parse(split[0].Trim()), double.Parse(split[1].Trim()));
        }

        var skillGroupParsed = double.TryParse(skillGroup, out var parsedValue);
        
        if (!skillGroupParsed)
        {
            return (double.NaN, double.NaN);
        }

        return (parsedValue, parsedValue + 0.5);
    }
}