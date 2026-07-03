using Newtonsoft.Json;
using System.Collections.Concurrent;
using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Helpers;
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Models.PbTournamentsModels;
using TournamentDuprRatings.Services;

namespace TournamentDuprRatings;

internal static class Program
{
    /// <summary>Maximum number of DUPR player lookups allowed to run concurrently.</summary>
    private const int MaxConcurrentLookups = 5;

    private static async Task<int> Main(string[] args)
    {
        LoadTestJsonResultIfProvided(args);

        var bearerToken = ResolveBearerToken(args);
        var tournamentName = ResolveTournamentName(args);

        var httpClient = new HttpClient();
        var tournamentService = new PickleballTournamentsService(httpClient);
        var duprService = new DuprService(httpClient, bearerToken);
        var htmlScraper = new PickleballPlayerScraper(httpClient);

        var fullTournamentDetails = await tournamentService.GetEventInfo(tournamentName);

        if (fullTournamentDetails.Events == null || fullTournamentDetails.Events.Count == 0)
        {
            Console.Error.WriteLine($"No events found for tournament: {tournamentName}");
            return 1;
        }

        // Cache of DUPR lookups shared across every bracket in the tournament.
        // Concurrent because lookups within a bracket now run in parallel (capped at MaxConcurrentLookups).
        var cachedDuprId = new ConcurrentDictionary<string, DuprPlayerInfo?>();
        var eventInfo = new List<EventInfo>();

        try
        {
            foreach (var group in fullTournamentDetails.Events)
            {
                if (group.Events == null || group.Events.Count == 0)
                {
                    Console.Error.WriteLine($"No brackets found for event: {group.GroupTitle}");
                    continue;
                }

                foreach (var bracket in group.Events)
                {
                    if (bracket.ActivityId == null)
                    {
                        Console.Error.WriteLine($"Skipping bracket {bracket.Title} due to missing ActivityId.");
                        continue;
                    }

                    var currentEvent = await ProcessBracketAsync(bracket, tournamentService, duprService, htmlScraper, cachedDuprId);
                    if (currentEvent != null)
                        eventInfo.Add(currentEvent);
                }
            }
        }
        catch (FatalProcessingException)
        {
            // The specific error was already written to the console by whichever step failed.
            return 1;
        }

        // Output
        var jsonOutput = JsonConvert.SerializeObject(eventInfo, Formatting.Indented);
        Console.WriteLine(jsonOutput);
        ExcelService.GenerateEventResultsExcel(eventInfo, tournamentName);

        return 0;
    }

    /// <summary>
    /// Fetches the roster for a single bracket, resolves each player's DUPR rating, and assembles
    /// the resulting teams. Returns null if the bracket has no players to process.
    /// </summary>
    private static async Task<EventInfo?> ProcessBracketAsync(
        TournamentEvent bracket,
        PickleballTournamentsService tournamentService,
        DuprService duprService,
        PickleballPlayerScraper htmlScraper,
        ConcurrentDictionary<string, DuprPlayerInfo?> cachedDuprId)
    {
        var currentEvent = new EventInfo
        {
            EventTitle = bracket.Title,
            SkillGroup = RatingCalculationHelpers.GetSkillGroup(bracket.SkillGroup),
            PlayerGroup = bracket.PlayerGroup,
            Format = bracket.Format,
            AgeGroup = bracket.AgeGroup
        };

        Console.WriteLine("Fetching tournament players...");

        List<EventPlayer> bracketPlayers;
        try
        {
            bracketPlayers = await tournamentService.GetEventPlayersAsync(bracket.ActivityId!);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching players: {ex.Message}");
            throw new FatalProcessingException();
        }

        if (bracketPlayers.Count == 0)
        {
            Console.Error.WriteLine($"No players found for {bracket.Title}");
            return null;
        }
        Console.WriteLine($"Found {bracketPlayers.Count} team entries.");

        var uniquePlayers = PlayerListBuilder.BuildUniquePlayerList(bracketPlayers);
        Console.WriteLine($"Unique players to look up: {uniquePlayers.Count}");

        var playerResults = await LookupPlayersAsync(uniquePlayers, htmlScraper, duprService, cachedDuprId);

        currentEvent.Teams.AddRange(BuildTeams(bracket, bracketPlayers, playerResults));

        return currentEvent;
    }

    /// <summary>
    /// Looks up DUPR ratings for each unique player. Lookups run in parallel, capped at
    /// <see cref="MaxConcurrentLookups"/> concurrent requests, using thread-safe caches.
    /// </summary>
    private static async Task<Dictionary<string, DuprPlayerInfo?>> LookupPlayersAsync(
        List<PlayerEntry> uniquePlayers,
        PickleballPlayerScraper htmlScraper,
        DuprService duprService,
        ConcurrentDictionary<string, DuprPlayerInfo?> cachedDuprId)
    {
        var playerResults = new ConcurrentDictionary<string, DuprPlayerInfo?>(StringComparer.OrdinalIgnoreCase);
        using var throttle = new SemaphoreSlim(MaxConcurrentLookups, MaxConcurrentLookups);

        var lookupTasks = uniquePlayers.Select(async player =>
        {
            if (player.Slug == null)
            {
                Console.WriteLine($"Skipping player {player.FullName} due to missing slug.");
                return;
            }

            await throttle.WaitAsync();
            try
            {
                playerResults[player.FullName] = await LookupSinglePlayerAsync(player, htmlScraper, duprService, cachedDuprId);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(lookupTasks);

        return new Dictionary<string, DuprPlayerInfo?>(playerResults, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a single player's DUPR profile, using the shared cache when the player's DUPR ID
    /// has already been looked up. Returns null if no rating could be found.
    /// </summary>
    private static async Task<DuprPlayerInfo?> LookupSinglePlayerAsync(
        PlayerEntry player,
        PickleballPlayerScraper htmlScraper,
        DuprService duprService,
        ConcurrentDictionary<string, DuprPlayerInfo?> cachedDuprId)
    {
        Console.WriteLine($"Looking up: https://pickleball.com/players/{player.Slug} ");

        var playerProfile = await htmlScraper.GetPlayerProfileAsync(player.Slug!);
        var playerInfo = new DuprPlayerInfo();

        try
        {
            if (cachedDuprId.TryGetValue(playerProfile.DuprId, out var cachedPlayerInfo))
            {
                return cachedPlayerInfo;
            }

            playerInfo = await duprService.GetPlayerInfo(playerProfile.DuprId);
        }
        catch (DuprUnauthorizedException)
        {
            Console.Error.WriteLine("\nError: Invalid or expired Bearer Token.");
            throw new FatalProcessingException();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nDUPR API error: {ex.Message}");
            throw new FatalProcessingException();
        }

        if (string.IsNullOrEmpty(playerInfo.Result.DuprId))
        {
            return null;
        }

        cachedDuprId.TryAdd(playerProfile.DuprId, playerInfo);
        Console.WriteLine($"Found: {playerInfo.Result.FullName}");
        return playerInfo;
    }

    /// <summary>
    /// Pairs each roster entry's player/partner with their resolved DUPR info to build the final team list.
    /// </summary>
    private static IEnumerable<TeamInfo> BuildTeams(
        TournamentEvent bracket,
        List<EventPlayer> bracketPlayers,
        Dictionary<string, DuprPlayerInfo?> playerResults)
    {
        foreach (var ep in bracketPlayers)
        {
            yield return new TeamInfo
            {
                EventTitle = bracket.Title,
                EventId = bracket.ActivityId,
                IsOnWaitList = ep.IsOnWaitlist,
                PlayerOne = BuildPlayerInfo(ep.PlayerFullName, ep.PlayerSlug, playerResults),
                PlayerTwo = BuildPlayerInfo(ep.PartnerFullName, ep.PartnerSlug, playerResults)
            };
        }
    }

    private static PlayerInfo BuildPlayerInfo(
        string? fullName,
        string? slug,
        Dictionary<string, DuprPlayerInfo?> playerResults)
    {
        var hit = OutputHelpers.ResolveHit(fullName, playerResults);
        return new PlayerInfo
        {
            FullName = fullName ?? "",
            DuprId = hit?.Result?.DuprId ?? "",
            Id = hit?.Result?.Id ?? 0,
            Slug = slug ?? "",
            DoublesDuprRating = GetPlayerRating(hit, true),
            SinglesDuprRating = GetPlayerRating(hit, false),
            Age = hit?.Result?.Age ?? 0
        };
    }

    /// <summary>Reads previously captured JSON document, for debugging.</summary>
    private static void LoadTestJsonResultIfProvided(string[] args)
    {
        var testJsonResult = GetArg(args, "test-json-result");
        if (string.IsNullOrWhiteSpace(testJsonResult))
        {
            return;
        }

        var testJsonFile = File.ReadAllText(testJsonResult);
        var results = JsonConvert.DeserializeObject<List<EventInfo>>(testJsonFile);
        if (results == null)
        {
            Console.Error.WriteLine($"Failed to deserialize test JSON result from: {testJsonResult}");
            return;
        }

        ExcelService.GenerateEventResultsExcel(results, "test");
    }

    private static string ResolveBearerToken(string[] args)
    {
        var bearerToken = GetArg(args, "bearer-token");
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            Console.Write("DUPR Bearer Token: ");
            bearerToken = OutputHelpers.ReadMaskedInput();
        }

        return bearerToken;
    }

    private static string ResolveTournamentName(string[] args)
    {
        var tournamentName = GetArg(args, "tournament-name");
        if (string.IsNullOrEmpty(tournamentName))
        {
            Console.Write("Tournament name (slug): ");
            tournamentName = Console.ReadLine()?.Trim() ?? "";
        }

        return tournamentName;
    }

    /// <summary>Signals that a step failed in a way that should stop the whole run (error already logged).</summary>
    private sealed class FatalProcessingException : Exception;

    private static double GetPlayerRating(DuprPlayerInfo? playerInfo, bool isDoubles)
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