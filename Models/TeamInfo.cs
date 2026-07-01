namespace TournamentDuprRatings.Models
{
    public class TeamInfo
    {
        public string? EventTitle { get; set; }
        public string? EventId { get; set; }
        public PlayerInfo? PlayerOne { get; set; }
        public PlayerInfo? PlayerTwo { get; set; }
        public double AverageTeamDupr => ((PlayerOne?.DoublesDuprRating ?? 0) + (PlayerTwo?.DoublesDuprRating ?? 0)) / 2.0;
        public string UniqueId => $"{PlayerOne?.DuprId}-{PlayerTwo?.DuprId}"; 
        public bool IsOnWaitList { get; set; }

    }
}
