using Newtonsoft.Json;
using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Helpers;
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Output;
using TournamentDuprRatings.Services;

namespace TournamentDuprRatings;

internal static class Program
{
    private static string _pickleBallTournamentsBaseUrl = PbbConstants.PickleBallTournamentsBaseUrl;
    private static async Task<int> Main(string[] args)
    {

        var testJsonResult = GetArg(args, "test-json-result");
        if (!string.IsNullOrWhiteSpace(testJsonResult))
        {
            var testJsonFile = File.ReadAllText(testJsonResult);
            //var results = JsonConvert.DeserializeObject<List<EventInfo>>(testJsonFile);
            //ExcelService.GenerateEventResultsExcel(results, "test");
        }

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
        if (string.IsNullOrEmpty(tournamentName))
        {
            Console.Write("Tournament name (slug): ");
            tournamentName = Console.ReadLine()?.Trim() ?? "";
        }

        var tournmanetCsvFileLocation = GetArg(args, "tournament-csv-file");
        if (string.IsNullOrEmpty(tournmanetCsvFileLocation))
        {
            Console.Write("Tournament CSV file path: ");
            tournmanetCsvFileLocation = Console.ReadLine()?.Trim() ?? "";
        }

        var csvService = new CsvService(tournmanetCsvFileLocation);
        var loadedCsvFile = csvService.LoadPlayersFromCsv();

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
        var outPutInfo = new List<EventResults>();
        var cachedDuprId = new Dictionary<string, DuprPlayerHit?>();
        var lowerBoundary = new Dictionary<string, List<DuprPlayerHit>>();
        var upperBoundary = new Dictionary<string, List<DuprPlayerHit>>();
        var failedPlayerLookup = new List<PlayerInfo>();
        var consolidatedTeamInfo = new List<TeamInfo>();
        var eventInfo = new List<EventInfo>();

        foreach (var group in fullTournamentDetails.Events)
        {
            var consolidatedTeam = new TeamInfo();
            foreach (var bracket in group.Events)
            {
                var skillGroup = RatingCalculationHelpers.GetSkillGroup(bracket.SkillGroup);
                var currentEvent = new EventInfo
                {
                    EventTitle = bracket.Title,
                    SkillGroup = skillGroup
                };

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
                            playerResults[player.FullName] = cachedDuprId[playerProfile.DuprId];
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
                        cachedDuprId.Add(playerProfile.DuprId, hits.FirstOrDefault());
                        Console.WriteLine($"Found: {hits.FirstOrDefault()?.FullName}");
                        var playerDupr = playerResults[player.FullName] = hits.FirstOrDefault();

                        if (skillGroup.lower != double.NaN && skillGroup.upper != double.NaN)
                        {
                            var rating = bracket.Format switch
                            {
                                "Doubles" => double.TryParse(playerDupr?.Ratings?.Doubles, out var doubles) ? doubles : 0.0,
                                "Singles" => double.TryParse(playerDupr?.Ratings?.Singles, out var singles) ? singles : 0.0,
                                _ => 0.0
                            };

                            RatingCalculationHelpers.CheckRatingBoundary(bracket.Title, rating, skillGroup, playerDupr, upperBoundary, lowerBoundary);
                        }
                    }

                    if (hits.Count == 0)
                    {
                        failedPlayerLookup.Add(new PlayerInfo
                        {
                            FullName = player.FullName,
                            DuprId = playerProfile.DuprId,
                            Slug = player.Slug,
                        });
                    }
                }

                // Assemble TeamResults
                var teams = new List<TeamResult>();
                foreach (var ep in tournaments[bracket.ActivityId])
                {
                    ep.PartnerFullName?.Trim();
                    //var team = new TeamResult
                    //{
                    //    Player1Name = ep.PlayerFullName ?? "",
                    //    Player1Doubles = OutputHelpers.ResolveRatingDisplay(ep.PlayerFullName, "Doubles", playerResults, skippedPlayers),
                    //    Player1Singles = OutputHelpers.ResolveRatingDisplay(ep.PlayerFullName, "Singles", playerResults, skippedPlayers),
                    //    Player1DuprId = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.DuprId,             
                    //    Player1PbbLink = $"{_pickleBallTournamentsBaseUrl}{ep.PlayerSlug}"
                    //};

                    //if (string.IsNullOrWhiteSpace(ep.PartnerFullName) || !ep.PartnerDuprActive)
                    //{
                    //    team.Player2Name = "N/A";
                    //    team.Player2DuprId = "N/A";
                    //    team.Player2Doubles = DoubleConstants.NotFoundRating;
                    //    team.Player2Singles = DoubleConstants.NotFoundRating;
                    //}
                    //else
                    //{
                    //    team.Player2Name = ep.PartnerFullName;
                    //    team.Player2Doubles = OutputHelpers.ResolveRatingDisplay(ep.PartnerFullName, "Doubles", playerResults, skippedPlayers);
                    //    team.Player2Singles = OutputHelpers.ResolveRatingDisplay(ep.PartnerFullName, "Singles", playerResults, skippedPlayers);
                    //    team.Player2DuprId = OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.DuprId;
                    //    team.Player2PbbLink = $"{_pickleBallTournamentsBaseUrl}{ep.PartnerSlug}";
                    //}

                    //team.SkillGroup = skillGroup;
                    //teams.Add(team);

                    currentEvent.Teams.Add(new TeamInfo
                    {
                        EventTitle = bracket.Title,
                        EventId = bracket.ActivityId,
                        PlayerOne = new PlayerInfo
                        {
                            FullName = ep.PlayerFullName ?? "",
                            DuprId = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.DuprId ?? "",
                            Slug = ep.PlayerSlug ?? "",
                            DoublesDuprRating = double.TryParse(OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.Ratings?.Doubles, out var doublesp1) ? doublesp1 : 0.0,
                            SinglesDuprRating = double.TryParse(OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.Ratings?.Singles, out var singlesp1) ? singlesp1 : 0.0,
                            Age = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.Age ?? 0
                        },
                        PlayerTwo = new PlayerInfo
                        {
                            FullName = ep.PartnerFullName ?? "",
                            DuprId = OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.DuprId ?? "",
                            Slug = ep.PartnerSlug ?? "",
                            DoublesDuprRating = double.TryParse(OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.Ratings?.Doubles, out var doublesp2) ? doublesp2 : 0.0,
                            SinglesDuprRating = double.TryParse(OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.Ratings?.Singles, out var singlesp2) ? singlesp2 : 0.0,
                            Age = OutputHelpers.ResolveHit(ep.PartnerFullName, playerResults)?.Age ?? 0
                        }
                    });
                }

                outPutInfo.Add(new EventResults
                {
                    SkillGroup = bracket.SkillGroup,
                    Title = bracket.Title,
                    TeamResults = teams
                });

                eventInfo.Add(currentEvent);
            }
        }

        // Output
        var jsonOutput = JsonConvert.SerializeObject(eventInfo, Formatting.Indented);
         ExcelService.GenerateEventResultsExcel(eventInfo, "test");
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