namespace TournamentDuprRatings.Models
{
    public class EventInfo
    {
        public required string EventTitle { get; set; }
        public (double lower, double upper) SkillGroup { get; set; }
        public List<TeamInfo> Teams { get; set; } = new List<TeamInfo>();
    }
}
