using ClosedXML.Excel;
using TournamentDuprRatings.Constants.Excel;
using TournamentDuprRatings.Models;
using TournamentDuprRatings.Services;
using Xunit;

namespace TournamentDuprRatings.Tests;

/// <summary>
/// Boundary-condition tests for the private-turned-internal DUPR rating color helpers in
/// <see cref="ExcelService"/>. Ratings are supplied explicitly (rather than going through a real
/// DUPR lookup) so each test can target one specific edge of the pass/fail thresholds.
/// </summary>
public class ExcelServiceRatingColorTests
{
    private const double Lower = 3.0;
    private const double Upper = 3.5; // Below ExcelRatingThresholds.OpenDivisionRatingThreshold (4.0), i.e. not an "Open" division.

    [Theory]
    [InlineData(0.0, Lower, 4.0, "NoRating")]  // Unrated player, division upper bound at the "Open" threshold.
    [InlineData(0.0, Lower, Upper, "Failed")]  // Unrated player, non-open division: falls through to the hard-floor check and fails.
    [InlineData(3.5, Lower, Upper, "Passed")]  // Exactly at the upper bound.
    [InlineData(3.51, Lower, Upper, "Failed")] // Just above the upper bound.
    [InlineData(2.5, Lower, Upper, "Passed")]  // Exactly at the hard floor (lower - 0.5).
    [InlineData(2.49, Lower, Upper, "Failed")] // Just below the hard floor.
    public void GetSinglesDuprCellColor_BoundaryConditions(double playerRating, double lower, double upper, string expected)
    {
        var result = ExcelService.GetSinglesDuprCellColor(playerRating, lower, upper);

        Assert.Equal(ExpectedColor(expected), result);
    }

    [Theory]
    [InlineData(0.0, 0.0, Lower, Upper, true, "Passed")]  // Both unrated, non-open division: passes.
    [InlineData(0.0, 0.0, 3.5, 4.0, true, "Failed")]      // Both unrated, division upper bound at the "Open" threshold: fails.
    [InlineData(0.0, 3.2, Lower, Upper, true, "Passed")]  // Player unrated, partner's rating covers (within range).
    [InlineData(0.0, 2.0, Lower, Upper, true, "Failed")]  // Player unrated, partner's rating does not cover (out of range).
    [InlineData(0.0, 4.2, 4.0, 4.5, true, "Failed")]      // Player unrated, division is above the "Open" threshold: always fails regardless of partner.
    [InlineData(3.6, 3.2, Lower, Upper, true, "Failed")]  // Player rated just above the upper bound.
    [InlineData(2.0, 3.2, Lower, Upper, true, "Failed")]  // Player rated below the hard floor (lower - 0.5 = 2.5).
    [InlineData(3.0, 1.0, Lower, Upper, true, "Passed")]  // Player exactly at the lower bound (partner's rating is irrelevant here).
    [InlineData(3.0, 1.0, Lower, Upper, false, "Passed")] // Same as above, but the player is "PlayerTwo" - confirms partner resolution still works.
    [InlineData(2.9, 3.2, Lower, Upper, true, "Passed")]  // Player slightly under the lower bound, partner's rating covers.
    [InlineData(2.9, 2.9, Lower, Upper, true, "Passed")]  // Player slightly under the lower bound, partner out of range but team average (2.9) clears the soft floor (2.85).
    [InlineData(2.9, 2.5, Lower, Upper, true, "Failed")]  // Player slightly under the lower bound, partner out of range and average below the soft floor.
    public void GetDoublesDuprCellColor_BoundaryConditions(
        double playerRating, double partnerRating, double lower, double upper, bool isPlayer1, string expected)
    {
        var team = isPlayer1
            ? new TeamInfo
            {
                PlayerOne = new PlayerInfo { DoublesDuprRating = playerRating },
                PlayerTwo = new PlayerInfo { DoublesDuprRating = partnerRating }
            }
            : new TeamInfo
            {
                PlayerOne = new PlayerInfo { DoublesDuprRating = partnerRating },
                PlayerTwo = new PlayerInfo { DoublesDuprRating = playerRating }
            };

        var result = ExcelService.GetDoublesDuprCellColor(playerRating, team, isPlayer1, lower, upper);

        Assert.Equal(ExpectedColor(expected), result);
    }

    private static XLColor ExpectedColor(string name) => name switch
    {
        "Passed" => ExcelPalette.PassedCheck,
        "Failed" => ExcelPalette.FailedCheck,
        "NoRating" => ExcelPalette.NoRatingCheck,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown expected color name.")
    };
}
