namespace TournamentDuprRatings.Models;

public class TeamResult
{
    public string UniqueId => $"{Player1DuprId}-{Player2DuprId}";
    public double AverageTeamDoublesDupr => (Player1Doubles + Player2Doubles ) / 2.0;
    public (double lower, double upper) SkillGroup { get; set; }

    public string Player1Name { get; set; } = "";
    public string? Player1DuprId { get; set; }
    public double Player1Doubles { get; set; }
    public double Player1Singles { get; set; }
    public string Player1PbbLink { get; set; } = "";

    public string Player2Name { get; set; } = "";
    public string? Player2DuprId { get; set; }
    public double Player2Doubles { get; set; }
    public double Player2Singles { get; set; }
    public string Player2PbbLink { get; set; } = "";
}
