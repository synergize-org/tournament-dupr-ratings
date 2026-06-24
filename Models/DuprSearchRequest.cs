namespace TournamentDuprRatings.Models;

public class DuprSearchRequest
{
    public int Limit { get; set; } = 10;
    public int Offset { get; set; } = 0;
    public string Query { get; set; } = "";
    public string[] Exclude { get; set; } = [];
    public bool IncludeUnclaimedPlayers { get; set; } = true;
    public DuprSearchFilter Filter { get; set; } = new();
}

public class DuprSearchFilter
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DuprRatingFilter Rating { get; set; } = new();
    public string LocationText { get; set; } = "";
}

public class DuprRatingFilter
{
    public double? MaxRating { get; set; }
    public double? MinRating { get; set; }
}
