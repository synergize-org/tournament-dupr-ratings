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
