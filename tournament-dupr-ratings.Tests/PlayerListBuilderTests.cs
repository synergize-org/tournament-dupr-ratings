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
