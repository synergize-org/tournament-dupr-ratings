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
