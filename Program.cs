using Newtonsoft.Json;
using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Helpers;
using TournamentDuprRatings.Models;
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

        var httpClient = new HttpClient();

        var tournamentService = new PickleballTournamentsService(httpClient);
        var fullTournamentDetails = await tournamentService.GetEventInfo(tournamentName);
        var tournaments = new Dictionary<string, List<EventPlayer>>();
        var outPutInfo = new List<EventResults>();
        var cachedDuprId = new Dictionary<string, DuprPlayerInfo?>();
        var lowerBoundary = new Dictionary<string, List<DuprPlayerInfo>>();
        var upperBoundary = new Dictionary<string, List<DuprPlayerInfo>>();
        var failedPlayerLookup = new List<PlayerInfo>();
        var consolidatedTeamInfo = new List<TeamInfo>();
        var eventInfo = new List<EventInfo>();
        var duprService = new DuprService(httpClient, bearerToken);
        var htmlScraper = new PickleballPlayerScraper();


        if (fullTournamentDetails.Events == null || fullTournamentDetails.Events.Count == 0)
        {
            Console.Error.WriteLine($"No events found for tournament: {tournamentName}");
            return 1;
        }
    
        foreach (var group in fullTournamentDetails.Events)
        {
            if (group.Events == null || group.Events.Count == 0)
            {
                Console.Error.WriteLine($"No brackets found for event: {group.GroupTitle}");
                continue;
            }
            
            var consolidatedTeam = new TeamInfo();
            foreach (var bracket in group.Events)
            {
                if (bracket.ActivityId == null)
                {
                    Console.Error.WriteLine($"Skipping bracket {bracket.Title} due to missing ActivityId.");
                    continue;
                }
            
                var skillGroup = RatingCalculationHelpers.GetSkillGroup(bracket.SkillGroup);
                var currentEvent = new EventInfo
                {
                    EventTitle = bracket.Title,
                    SkillGroup = skillGroup,
                    PlayerGroup = bracket.PlayerGroup,
                    Format = bracket.Format,             
                    AgeGroup = bracket.AgeGroup
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
                var playerResults = new Dictionary<string, DuprPlayerInfo?>(StringComparer.OrdinalIgnoreCase);
                var skippedPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var player in uniquePlayers)
                {
                    if (player.Slug == null)
                    {
                        Console.WriteLine($"Skipping player {player.FullName} due to missing slug.");
                        continue;
                    }
                
                    Console.WriteLine($"Looking up: https://pickleball.com/players/{player.Slug} ");

                    var playerProfile = await htmlScraper.GetPlayerProfileAsync(player.Slug);                    

                    DuprPlayerInfo playerInfo = new DuprPlayerInfo();
              
                    try
                    {
                        if (cachedDuprId.ContainsKey(playerProfile.DuprId))
                        {
                            playerResults[player.FullName] = cachedDuprId[playerProfile.DuprId];
                        }
                        else
                        {
                            playerInfo = await duprService.GetPlayerInfo(playerProfile.DuprId);
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

                    if (playerInfo != null && !string.IsNullOrEmpty(playerInfo.Result.DuprId))
                    {
                        cachedDuprId.Add(playerProfile.DuprId, playerInfo);
                        Console.WriteLine($"Found: {playerInfo.Result.FullName}");
                        var playerDupr = playerResults[player.FullName] = playerInfo;

                        if (skillGroup.lower != double.NaN && skillGroup.upper != double.NaN)
                        {
                            var rating = bracket.Format switch
                            {
                                "Doubles" => GetPlayerRating(playerDupr, true),
                                "Singles" => GetPlayerRating(playerDupr, false),
                                _ => DoubleConstants.NoRating
                            };

                            RatingCalculationHelpers.CheckRatingBoundary(bracket.Title, rating, skillGroup, playerDupr, upperBoundary, lowerBoundary);
                        }
                    }

                    if (playerInfo == null || string.IsNullOrEmpty(playerInfo.Result.DuprId))
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
                    currentEvent.Teams.Add(new TeamInfo
                    {
                        EventTitle = bracket.Title,
                        EventId = bracket.ActivityId,
                        IsOnWaitList = ep.IsOnWaitlist,
                        PlayerOne = new PlayerInfo
                        {
                            FullName = ep.PlayerFullName ?? "",
                            DuprId = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.Result?.DuprId ?? "",
                            Id = OutputHelpers.ResolveHit(ep.PlayerFullName, playerResults)?.Result?.Id ?? 0,
                            Slug = ep.PlayerSlug ?? "",
                            DoublesDuprRating = GetPlayerRating(OutputHelpers.ResolveHit(ep?.PlayerFullName, playerResults), true),
                            SinglesDuprRating = GetPlayerRating(OutputHelpers.ResolveHit(ep?.PlayerFullName, playerResults), false),
                            Age = OutputHelpers.ResolveHit(ep?.PlayerFullName, playerResults)?.Result?.Age ?? 0
                        },
                        PlayerTwo = new PlayerInfo
                        {
                            FullName = ep?.PartnerFullName ?? "",
                            DuprId = OutputHelpers.ResolveHit(ep?.PartnerFullName, playerResults)?.Result?.DuprId ?? "",
                            Id = OutputHelpers.ResolveHit(ep?.PartnerFullName, playerResults)?.Result?.Id ?? 0,
                            Slug = ep?.PartnerSlug ?? "",
                            DoublesDuprRating = GetPlayerRating(OutputHelpers.ResolveHit(ep?.PartnerFullName, playerResults), true),
                            SinglesDuprRating = GetPlayerRating(OutputHelpers.ResolveHit(ep?.PartnerFullName, playerResults), false),
                            Age = OutputHelpers.ResolveHit(ep?.PartnerFullName, playerResults)?.Result?.Age ?? 0
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
        Console.WriteLine(jsonOutput);
        ExcelService.GenerateEventResultsExcel(eventInfo, tournamentName);

        return 0;
    }

    private static double GetPlayerRating(DuprPlayerInfo playerInfo, bool isDoubles)
    {
        if (playerInfo == null)
        {
            return DoubleConstants.NotFoundRating;
        }

        const string noRating = "NR";
        if (isDoubles)
        {
            if (playerInfo.Result.Ratings.Doubles != null && playerInfo.Result.Ratings.Doubles != noRating)
            {
                return double.Parse(playerInfo.Result.Ratings.Doubles);
            }

            if (playerInfo.Result.Ratings.ProvisionalRatings.DoublesRating.HasValue)
            {
                return playerInfo.Result.Ratings.ProvisionalRatings.DoublesRating.Value;
            }
        }
    
        else
        {
            if (playerInfo.Result.Ratings.Singles != null && playerInfo.Result.Ratings.Singles != noRating)
            {
                return double.Parse(playerInfo.Result.Ratings.Singles);
            }

            if (playerInfo.Result.Ratings.ProvisionalRatings.SinglesRating.HasValue)
            {
                return playerInfo.Result.Ratings.ProvisionalRatings.SinglesRating.Value;
            }
        }

        return DoubleConstants.NoRating; // Return NotFoundRating if no rating is found
    }

    private static string? GetArg(string[] args, string name)
    {
        var flag = $"--{name}";
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}