using ClosedXML.Excel;
using System.Text.RegularExpressions;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services
{
    public class ExcelService
    {
        public static void GenerateEventResultsExcel(List<EventInfo> eventInfo, string fileName)
        {
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{fileName}.xlsx");
            using var workbook = File.Exists(filePath)
                ? new XLWorkbook(filePath)
                : new XLWorkbook();

            foreach (var tournamentInfo in eventInfo)
            {
                var sheetName = SanitizeSheetName(tournamentInfo.EventTitle);

                if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
                    sheet = CreateSheet(workbook, sheetName, tournamentInfo.EventTitle);

                UpsertTeamRows(sheet, tournamentInfo);
                sheet.Columns().AdjustToContents();
                sheet.Column(1).Width = 8;
            }

            workbook.SaveAs(filePath);
        }

        private static void UpsertTeamRows(IXLWorksheet sheet, EventInfo eventInfo)
        {
            // Build a lookup of UniqueId -> row number from the existing sheet
            // UniqueId is stored in a hidden column (col 10) to avoid parsing display data
            var existingRows = new Dictionary<string, int>();

            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 2;
            for (int row = 3; row <= lastRow; row++)
            {
                var idCell = sheet.Cell(row, 11).GetString();
                if (!string.IsNullOrEmpty(idCell))
                    existingRows[idCell] = row;
            }

            foreach (var team in eventInfo.Teams)
            {
                if (existingRows.TryGetValue(team.UniqueId, out int existingRow))
                {
                    WriteTeamRow(sheet, existingRow, team, existingRow % 2 != 0, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
                }
                else
                {
                    int newRow = (lastRow < 2 ? 2 : lastRow) + 1;
                    WriteTeamRow(sheet, newRow, team, newRow % 2 != 0, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
                    existingRows[team.UniqueId] = newRow;
                    lastRow = newRow;
                }
            }

            // Re-number the Place column sequentially after all upserts
            int place = 1;
            for (int row = 3; row <= lastRow; row++)
            {
                if (!string.IsNullOrEmpty(sheet.Cell(row, 11).GetString()))
                    sheet.Cell(row, 1).Value = place++;
            }
        }

        private static void WriteTeamRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            var rowRange = sheet.Range(row, 1, row, 9);
            rowRange.Style.Fill.BackgroundColor = isEvenRow
                ? XLColor.FromArgb(235, 241, 250)
                : XLColor.NoColor;

            sheet.Cell(row, 2).Value = team.PlayerOne.FullName;
            if (!string.IsNullOrEmpty(team.PlayerOne.PbbLink))
            {
               sheet.Cell(row, 2).SetHyperlink(new XLHyperlink(team.PlayerOne.PbbLink));
            }
            sheet.Cell(row, 3).Value = team.PlayerOne.DuprId ?? "-";
            if (!string.IsNullOrEmpty(team.PlayerOne.DuprId))
            {
                sheet.Cell(row, 3).SetHyperlink(new XLHyperlink($"https://dashboard.dupr.com/dashboard/player/{team.PlayerOne.DuprId}"));
            }
            sheet.Cell(row, 4).Value = team.PlayerOne.DoublesDuprRating;
            sheet.Cell(row, 5).Value = team.PlayerOne.SinglesDuprRating;

            sheet.Cell(row, 6).Value = team.PlayerTwo.FullName;
            if (!string.IsNullOrEmpty(team.PlayerTwo.PbbLink))
            {
                sheet.Cell(row, 6).SetHyperlink(new XLHyperlink(team.PlayerTwo.PbbLink));
            }
            sheet.Cell(row, 7).Value = team.PlayerTwo.DuprId ?? "-";
            if (!string.IsNullOrEmpty(team.PlayerTwo.DuprId))
            {
                sheet.Cell(row, 7).SetHyperlink(new XLHyperlink($"https://dashboard.dupr.com/dashboard/player/{team.PlayerTwo.DuprId}"));
            }
            sheet.Cell(row, 8).Value = team.PlayerTwo.DoublesDuprRating;
            sheet.Cell(row, 9).Value = team.PlayerTwo.SinglesDuprRating;

            // Team avg doubles
            var avgCell = sheet.Cell(row, 10);
            avgCell.Value = team.AverageTeamDupr;
            avgCell.Style.NumberFormat.Format = "0.000";

            sheet.Cell(row, 4).Style.Fill.BackgroundColor = GetDuprCellColor(team.PlayerOne.DoublesDuprRating, team, isPlayer1: true, lowerSkillRating, upperSkillRating);
            if (string.IsNullOrEmpty(team.PlayerTwo.FullName.Trim()))
            {
                sheet.Cell(row, 8).Style.Fill.BackgroundColor = XLColor.Salmon;
            }
            else {
                sheet.Cell(row, 8).Style.Fill.BackgroundColor = GetDuprCellColor(team.PlayerTwo.DoublesDuprRating, team, isPlayer1: false, lowerSkillRating, upperSkillRating);
            }

            foreach (int ratingCol in new[] { 4, 5, 8, 9 })
                sheet.Cell(row, ratingCol).Style.NumberFormat.Format = "0.000";

            // On Waitlist
            var waitlistCell = sheet.Cell(row, 11);
            waitlistCell.Value = team.IsOnWaitList ? "Yes" : "No";

            // Store UniqueId in hidden column 12 as the stable key
            var idCell = sheet.Cell(row, 12);
            idCell.Value = team.UniqueId;
            idCell.Style.Fill.BackgroundColor = XLColor.NoColor;
            sheet.Column(12).Hide();
        }

        private static IXLWorksheet CreateSheet(XLWorkbook workbook, string sheetName, string title)
        {
            var sheet = workbook.Worksheets.Add(sheetName);

            var titleCell = sheet.Cell(1, 1);
            titleCell.Value = title;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            titleCell.Style.Font.FontColor = XLColor.White;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] value = [
                "Teams",
        "Player 1 Name", "Player 1 DUPR ID", "Player 1 Doubles", "Player 1 Singles",
        "Player 2 Name", "Player 2 DUPR ID", "Player 2 Doubles", "Player 2 Singles", "Average Team DUPR", "On Waitlist"
            ];
            string[] headers =
            value;

            sheet.Range(1, 1, 1, headers.Length + 1).Merge();

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = sheet.Cell(2, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            return sheet;
        }

        private static string SanitizeSheetName(string name)
        {
            // Attempt to parse the known format:
            // "{Gender Group} - Age: ({age}) - DUPR {skillGroup}"
            // e.g. "Mixed Doubles - Age: (12+) - DUPR 3.5 to 4.0"
            //   -> "Mixed Doubles 12+ 3.5-4.0"
            var match = Regex.Match(name,
                @"^(?<gender>.+?)\s*-\s*Age:\s*\((?<age>[^)]+)\)\s*-\s*DUPR\s*(?<dupr>.+)$",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var gender = match.Groups["gender"].Value.Trim();
                var age = match.Groups["age"].Value.Trim();
                var dupr = match.Groups["dupr"].Value.Trim().Replace(" to ", "-");

                // "Mixed Doubles 12+ 3.5-4.0" = 26 chars, well within 31
                name = $"{gender} {age} {dupr}";
            }

            // Fallback sanitization for anything that doesn't match the pattern
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();

            return sanitized.Length > 31 ? sanitized[..31] : sanitized;
        }

        private static XLColor GetDuprCellColor(double playerDoubles, TeamInfo team, bool isPlayer1, double lowerSkillRating, double upperSkillRating)
        {
            double lower = lowerSkillRating;
            double upper = upperSkillRating;    
            double hardFloor = lower - 0.500;
            double softFloor = lower - 0.150;

            double partnerDoubles = isPlayer1 ? team.PlayerTwo.DoublesDuprRating : team.PlayerOne.DoublesDuprRating;
            bool playerUnrated = playerDoubles == 0.0;
            bool partnerUnrated = partnerDoubles == 0.0;

            // Both players are unrated, can play under 4.0, but not above          
            if (team.PlayerOne.DoublesDuprRating == 0.0 && team.PlayerTwo.DoublesDuprRating == 0.0) 
            {
                if (upperSkillRating >= 4.0)
                    return XLColor.Salmon;
                else
                    return XLColor.White;
            }        

            // Handle any unrated player on the team — both cells are affected
            if (playerUnrated || partnerUnrated)
                return GetUnratedColor(playerDoubles, partnerDoubles, lower, upper);

            // Always red if above upper
            if (playerDoubles > upper)
                return XLColor.Salmon;

            // Always red if below hard floor, no exceptions
            if (playerDoubles < hardFloor)
                return XLColor.Salmon;

            // Within proper range, no further checks needed
            if (playerDoubles >= lower)
                return XLColor.White;

            // Player is playing up (between hardFloor and lower)
            bool partnerInRange = partnerDoubles >= lower && partnerDoubles <= upper;
            bool teamAvgAcceptable = team.AverageTeamDupr >= softFloor;

            if (partnerInRange || teamAvgAcceptable)
                return XLColor.White;

            return XLColor.Salmon;
        }

        private static XLColor GetUnratedColor(double playerDoubles, double partnerDoubles, double lower, double upper)
        {
            // Unrated players cannot compete in any division above 4.0
            if (upper > 4.0)
                return XLColor.Salmon;

            // The rated partner must be within the skill range
            double ratedPlayerDoubles = playerDoubles == 0.0 ? partnerDoubles : playerDoubles;
            bool ratedPartnerInRange = ratedPlayerDoubles >= lower && ratedPlayerDoubles <= upper;

            return ratedPartnerInRange ? XLColor.White : XLColor.Salmon;
        }
    }
}