using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Constants.Excel
{
    /// <summary>
    /// Maps each result field to the worksheet columns it occupies. Every visible field spans
    /// two physical columns (merged) so headers/values line up cleanly; each layout also
    /// reserves one trailing hidden column that stores the team's <see cref="TeamInfo.UniqueId"/>.
    /// </summary>
    public static class ExcelColumns
    {
        public const int ColumnSpan = 2;

        /// <summary>Place/rank column - shared by both the singles and doubles layouts.</summary>
        public const int Place = 1;

        public static class Doubles
        {
            public const int Player1Name = Place + ColumnSpan;
            public const int Player1DuprId = Player1Name + ColumnSpan;
            public const int Player1Rating = Player1DuprId + ColumnSpan;
            public const int Player2Name = Player1Rating + ColumnSpan;
            public const int Player2DuprId = Player2Name + ColumnSpan;
            public const int Player2Rating = Player2DuprId + ColumnSpan;
            public const int AverageTeamDupr = Player2Rating + ColumnSpan;
            public const int OnWaitlist = AverageTeamDupr + ColumnSpan;
            public const int UniqueId = OnWaitlist + ColumnSpan;
            public const int VisibleColumnCount = UniqueId - 1;
        }

        public static class Singles
        {
            public const int PlayerName = Place + ColumnSpan;
            public const int PlayerDuprId = PlayerName + ColumnSpan;
            public const int PlayerRating = PlayerDuprId + ColumnSpan;
            public const int OnWaitlist = PlayerRating + ColumnSpan;
            public const int UniqueId = OnWaitlist + ColumnSpan;
            public const int VisibleColumnCount = UniqueId - 1;
        }
    }
}
