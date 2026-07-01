using TournamentDuprRatings.Constants;

namespace TournamentDuprRatings.Models
{
    public class PlayerInfo
    {
        public string? FullName { get; set; }
        public string? DuprId { get; set; }
        public long Id { get; set; }
        public string? Slug { get; set; }
        public double DoublesDuprRating { get; set; }
        public double SinglesDuprRating { get; set; }
        public int Age { get; set; }
        public string PbbLink => $"{PbbConstants.PickleBallTournamentsBaseUrl}{Slug}";
    }
}
