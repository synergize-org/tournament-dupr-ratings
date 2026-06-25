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
    public long? Id { get; set; }
    public string? FullName { get; set; }
    public string? ShortAddress { get; set; }
    public int? Age { get; set; }
    public string? DuprId { get; set; }
    public DuprRatings? Ratings { get; set; }
}

public class DuprRatings
{
    public string? Doubles { get; set; }
    public string? DoublesVerified { get; set; }
    public string? Singles { get; set; }
    public string? SinglesVerified { get; set; }

    public double DecimalDoublesDupr()
    {
        if (double.TryParse(Doubles, out var result))
            return result;
        return 0.0;
    }

    public double DecimalSinglesDupr()
    {
        if (double.TryParse(Singles, out var result))
            return result;
        return 0.0;
    }
}
