using TournamentDuprRatings.Helpers;
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
            bearerToken = OutputHelpers.ReadMaskedInput();
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
                var skillGroup = RatingCalculationHelpers.GetSkillGroup(bracket.SkillGroup);

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
                            var rating = bracket.Format switch
                            {
                                "Doubles" => double.TryParse(playerDupr?.Ratings?.Doubles, out var doubles) ? doubles : (double?)null,
                                "Singles" => double.TryParse(playerDupr?.Ratings?.Singles, out var singles) ? singles : (double?)null,
                                _ => null
                            };

                            RatingCalculationHelpers.CheckRatingBoundary(bracket.Title, rating, skillGroup, playerDupr, upperBoundary, lowerBoundary);
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
                        Player1Doubles = OutputHelpers.ResolveRatingDisplay(ep.PlayerFullName, "Doubles", playerResults, skippedPlayers),
                        Player1Singles = OutputHelpers.ResolveRatingDisplay(ep.PlayerFullName, "Singles", playerResults, skippedPlayers),
                        Player1DuprId = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.DuprId
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
                        team.Player2Doubles = OutputHelpers.ResolveRatingDisplay(ep.PartnerFullName, "Doubles", playerResults, skippedPlayers);
                        team.Player2Singles = OutputHelpers.ResolveRatingDisplay(ep.PartnerFullName, "Singles", playerResults, skippedPlayers);
                        team.Player2DuprId = OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.DuprId;
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

    private static string? GetArg(string[] args, string name)
    {
        var flag = $"--{name}";
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}