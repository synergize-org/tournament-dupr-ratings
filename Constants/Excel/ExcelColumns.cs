using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Constants.Excel
{
    /// <summary>
    /// Maps each result field to the single worksheet column it occupies. Per Microsoft's guidance
    /// on organizing worksheet data, each field gets exactly one column (no merged cells, so column
    /// auto-sizing and sorting/filtering behave correctly), and every column - including the
    /// internal <see cref="TeamInfo.UniqueId"/> lookup column - stays visible rather than hidden.
    /// </summary>
    public static class ExcelColumns
    {
        /// <summary>Place/rank column - shared by both the singles and doubles layouts.</summary>
        public const int Place = 1;

        public static class Doubles
        {
            public const int Player1Name = Place + 1;
            public const int Player1DuprId = Player1Name + 1;
            public const int Player1Rating = Player1DuprId + 1;
            public const int Player2Name = Player1Rating + 1;
            public const int Player2DuprId = Player2Name + 1;
            public const int Player2Rating = Player2DuprId + 1;
            public const int AverageTeamDupr = Player2Rating + 1;
            public const int OnWaitlist = AverageTeamDupr + 1;
            public const int InternalId = OnWaitlist + 1;
            public const int TotalColumnCount = InternalId;
        }

        public static class Singles
        {
            public const int PlayerName = Place + 1;
            public const int PlayerDuprId = PlayerName + 1;
            public const int PlayerRating = PlayerDuprId + 1;
            public const int OnWaitlist = PlayerRating + 1;
            public const int InternalId = OnWaitlist + 1;
            public const int TotalColumnCount = InternalId;
        }
    }
}
