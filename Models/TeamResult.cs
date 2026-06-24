namespace TournamentDuprRatings.Models;

public class TeamResult
{
    public string Player1Name { get; set; } = "";
    public string? Player1DuprId { get; set; }
    public string Player1Doubles { get; set; } = "";
    public string Player1Singles { get; set; } = "";

    public string Player2Name { get; set; } = "";
    public string? Player2DuprId { get; set; }
    public string Player2Doubles { get; set; } = "";
    public string Player2Singles { get; set; } = "";
}
