namespace TournamentDuprRatings.Constants.Excel
{
    /// <summary>Rating thresholds that determine how a player's DUPR rating is judged against a division.</summary>
    public static class ExcelRatingThresholds
    {
        /// <summary>
        /// Divisions whose upper skill bound is at or above this value are effectively "Open"
        /// divisions, where an unrated player is more likely a data problem than a legitimately new player.
        /// </summary>
        public const double OpenDivisionRatingThreshold = 4.0;

        /// <summary>
        /// A player's rating can fall at most this far below the division's lower bound before
        /// automatically failing, regardless of partner rating or team average.
        /// </summary>
        public const double HardFloorMargin = 0.500;

        /// <summary>
        /// When a player is slightly under the lower bound, the team average must be within this
        /// margin of the lower bound for the pairing to still be considered acceptable.
        /// </summary>
        public const double SoftFloorMargin = 0.150;
    }
}
