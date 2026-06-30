using ClosedXML.Excel;
using System.Text.RegularExpressions;
using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services
{
    public class ExcelService
    {
        // Doubles column map (each value spans 2 columns)
        // 1-2: Place, 3-4: P1 Name, 5-6: P1 DUPR ID, 7-8: P1 Doubles
        // 9-10: P2 Name, 11-12: P2 DUPR ID, 13-14: P2 Doubles
        // 15-16: Avg Team DUPR, 17-18: On Waitlist, 19: UniqueId (hidden)
        private const int DoublesColCount = 18;
        private const int DoublesIdCol = 19;

        // Singles column map (each value spans 2 columns)
        // 1-2: Place, 3-4: P1 Name, 5-6: P1 DUPR ID, 7-8: P1 Singles, 9-10: On Waitlist, 11: UniqueId (hidden)
        private const int SinglesColCount = 10;
        private const int SinglesIdCol = 11;

        private static readonly XLColor _passedCheckColor = XLColor.White;
        private static readonly XLColor _failedCheckColor = XLColor.Salmon;
        private static readonly XLColor _noPartnerCheckColor = XLColor.Flavescent;
        private static readonly XLColor _noRatingCheckColor = XLColor.DarkOrchid;

        public static void GenerateEventResultsExcel(List<EventInfo> eventInfo, string fileName)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"{fileName}_{timestamp}.xlsx");

            using var workbook = new XLWorkbook();

            var consolidatedSheets = eventInfo
                .GroupBy(e => $"{e.PlayerGroup} {e.Format} - {e.AgeGroup}")
                .ToList();

            foreach (var sheetGroup in consolidatedSheets)
            {
                var sheetName = SanitizeSheetName(sheetGroup.Key);
                var sheet = workbook.Worksheets.Add(sheetName);
                sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                bool isSingles = sheetGroup.First().Format.Equals("Singles", StringComparison.OrdinalIgnoreCase);

                int currentRow = 1;
                foreach (var tournamentInfo in sheetGroup)
                {
                    currentRow = WriteEventSection(sheet, tournamentInfo, currentRow, isSingles);
                    currentRow += 2; // Empty row between divisions
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(1).Width = 8;
                sheet.Column(2).Width = 8;
            }

            UpdateSummarySheet(workbook, eventInfo);
            workbook.SaveAs(filePath);
        }

        private static int WriteEventSection(IXLWorksheet sheet, EventInfo eventInfo, int startRow, bool isSingles)
        {
            int colCount = isSingles ? SinglesColCount : DoublesColCount;

            // Section title header — spans all columns
            var titleCell = sheet.Cell(startRow, 1);
            titleCell.Value = eventInfo.EventTitle;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            titleCell.Style.Font.FontColor = _passedCheckColor;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(startRow, 1, startRow, colCount).Merge();
            startRow++;

            // Column headers — each spans 2 columns
            string[] headers = isSingles
                ? ["Place", "Player Name", "DUPR ID", "Singles DUPR", "On Waitlist"]
                : ["Place", "Player 1 Name", "Player 1 DUPR ID", "Player 1 Doubles", "Player 2 Name", "Player 2 DUPR ID", "Player 2 Doubles", "Average Team DUPR", "On Waitlist"];

            for (int i = 0; i < headers.Length; i++)
            {
                int col = (i * 2) + 1;
                var cell = sheet.Cell(startRow, col);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = _passedCheckColor;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                sheet.Range(startRow, col, startRow, col + 1).Merge();
            }
            startRow++;

            // Team rows
            int place = 1;
            foreach (var team in eventInfo.Teams)
            {
                bool isEvenRow = place % 2 == 0;
                if (isSingles)
                    WriteSinglesRow(sheet, startRow, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
                else
                    WriteDoublesRow(sheet, startRow, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);

                // Place spans cols 1-2
                sheet.Cell(startRow, 1).Value = place++;
                sheet.Range(startRow, 1, startRow, 2).Merge();

                startRow++;
            }

            return startRow - 1;
        }

        private static void WriteSinglesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {              
            var rowRange = sheet.Range(row, 1, row, SinglesColCount);
            rowRange.Style.Fill.BackgroundColor = isEvenRow
                ? XLColor.FromArgb(235, 241, 250)
                : XLColor.NoColor;

            // P1 Name (cols 3-4)
            sheet.Cell(row, 3).Value = team.PlayerOne.FullName;
            if (!string.IsNullOrEmpty(team.PlayerOne.PbbLink))
                sheet.Cell(row, 3).SetHyperlink(new XLHyperlink(team.PlayerOne.PbbLink));
            sheet.Range(row, 3, row, 4).Merge();

            // P1 DUPR ID (cols 5-6)
            sheet.Cell(row, 5).Value = team.PlayerOne.DuprId ?? "-";
            if (!string.IsNullOrEmpty(team.PlayerOne.DuprId))
                sheet.Cell(row, 5).SetHyperlink(new XLHyperlink($"https://dashboard.dupr.com/dashboard/player/{team.PlayerOne.Id}"));
            sheet.Range(row, 5, row, 6).Merge();

            // P1 Singles DUPR (cols 7-8)
            sheet.Cell(row, 7).Value = team.PlayerOne.SinglesDuprRating;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "0.000";
            sheet.Cell(row, 7).Style.Fill.BackgroundColor = GetSinglesDuprCellColor(team.PlayerOne.SinglesDuprRating, lowerSkillRating, upperSkillRating);
            sheet.Range(row, 7, row, 8).Merge();

            // On Waitlist (cols 9-10)
            sheet.Cell(row, 9).Value = team.IsOnWaitList ? "Yes" : "No";
            sheet.Range(row, 9, row, 10).Merge();

            // Hidden UniqueId (col 11)
            var idCell = sheet.Cell(row, SinglesIdCol);
            idCell.Value = team.UniqueId;
            idCell.Style.Fill.BackgroundColor = XLColor.NoColor;
            sheet.Column(SinglesIdCol).Hide();
        }

        private static void WriteDoublesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            bool missingPartner = string.IsNullOrEmpty(team.PlayerTwo.FullName.Trim());

            var rowRange = sheet.Range(row, 1, row, DoublesColCount);
            rowRange.Style.Fill.BackgroundColor = isEvenRow
                ? XLColor.FromArgb(235, 241, 250)
                : XLColor.NoColor;

            // P1 Name (cols 3-4)
            sheet.Cell(row, 3).Value = team.PlayerOne.FullName;
            if (!string.IsNullOrEmpty(team.PlayerOne.PbbLink))
                sheet.Cell(row, 3).SetHyperlink(new XLHyperlink(team.PlayerOne.PbbLink));
            sheet.Range(row, 3, row, 4).Merge();

            // P1 DUPR ID (cols 5-6)
            sheet.Cell(row, 5).Value = team.PlayerOne.DuprId ?? "-";
            if (!string.IsNullOrEmpty(team.PlayerOne.DuprId))
                sheet.Cell(row, 5).SetHyperlink(new XLHyperlink($"https://dashboard.dupr.com/dashboard/player/{team.PlayerOne.Id}"));
            sheet.Range(row, 5, row, 6).Merge();

            // P1 Doubles DUPR (cols 7-8)
            sheet.Cell(row, 7).Value = team.PlayerOne.DoublesDuprRating;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "0.000";
            sheet.Cell(row, 7).Style.Fill.BackgroundColor = missingPartner
                ? _noPartnerCheckColor
                : GetDuprCellColor(team.PlayerOne.DoublesDuprRating, team, isPlayer1: true, lowerSkillRating, upperSkillRating);
            sheet.Range(row, 7, row, 8).Merge();

            if (sheet.Cell(row, 7).Style.Fill.BackgroundColor == _passedCheckColor && team.PlayerOne.DoublesDuprRating == DoubleConstants.NotFoundRating)
            {
                sheet.Cell(row, 7).Style.Fill.BackgroundColor = _noRatingCheckColor;
            }

            // P2 Name (cols 9-10)
            sheet.Cell(row, 9).Value = team.PlayerTwo.FullName;
            if (!string.IsNullOrEmpty(team.PlayerTwo.PbbLink))
                sheet.Cell(row, 9).SetHyperlink(new XLHyperlink(team.PlayerTwo.PbbLink));
            sheet.Range(row, 9, row, 10).Merge();

            // P2 DUPR ID (cols 11-12)
            sheet.Cell(row, 11).Value = team.PlayerTwo.DuprId ?? "-";
            if (!string.IsNullOrEmpty(team.PlayerTwo.DuprId))
                sheet.Cell(row, 11).SetHyperlink(new XLHyperlink($"https://dashboard.dupr.com/dashboard/player/{team.PlayerTwo.Id}"));
            sheet.Range(row, 11, row, 12).Merge();

            // P2 Doubles DUPR (cols 13-14)
            sheet.Cell(row, 13).Value = team.PlayerTwo.DoublesDuprRating;
            sheet.Cell(row, 13).Style.NumberFormat.Format = "0.000";
            sheet.Cell(row, 13).Style.Fill.BackgroundColor = missingPartner
                ? _noPartnerCheckColor
                : GetDuprCellColor(team.PlayerTwo.DoublesDuprRating, team, isPlayer1: false, lowerSkillRating, upperSkillRating);
            sheet.Range(row, 13, row, 14).Merge();

            if (sheet.Cell(row, 13).Style.Fill.BackgroundColor == _passedCheckColor && team.PlayerTwo.DoublesDuprRating == DoubleConstants.NotFoundRating)
            {
                sheet.Cell(row, 13).Style.Fill.BackgroundColor = _noRatingCheckColor;
            }

            // Avg Team DUPR (cols 15-16)
            sheet.Cell(row, 15).Value = team.AverageTeamDupr;
            sheet.Cell(row, 15).Style.NumberFormat.Format = "0.000";
            sheet.Range(row, 15, row, 16).Merge();

            // On Waitlist (cols 17-18)
            sheet.Cell(row, 17).Value = team.IsOnWaitList ? "Yes" : "No";
            sheet.Range(row, 17, row, 18).Merge();

            // Hidden UniqueId (col 19)
            var idCell = sheet.Cell(row, DoublesIdCol);
            idCell.Value = team.UniqueId;
            idCell.Style.Fill.BackgroundColor = XLColor.NoColor;
            sheet.Column(DoublesIdCol).Hide();
        }

        private static void UpdateSummarySheet(XLWorkbook workbook, List<EventInfo> eventInfo)
        {
            const string summaryName = "Summary";

            var summary = workbook.Worksheets.Add(summaryName);
            workbook.Worksheets.Worksheet(summaryName).Position = 1;

            // Title header
            var titleCell = summary.Cell(1, 1);
            titleCell.Value = "Event Summary";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 16;
            titleCell.Style.Font.FontColor = _passedCheckColor;
            titleCell.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            summary.Range(1, 1, 1, 2).Merge();

            // Color key header
            int currentRow = 2;
            var keyHeaderCell = summary.Cell(currentRow, 1);
            keyHeaderCell.Value = "Color Key";
            keyHeaderCell.Style.Font.Bold = true;
            keyHeaderCell.Style.Font.FontSize = 12;
            keyHeaderCell.Style.Font.FontColor = _passedCheckColor;
            keyHeaderCell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
            keyHeaderCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            summary.Range(currentRow, 1, currentRow, 2).Merge();
            currentRow++;

            var colorKeys = new[]
            {
        (_passedCheckColor,  "Player DUPR is within the required range"),
        (_failedCheckColor, "Player DUPR does not meet division requirements"),
        (_noPartnerCheckColor, "Player has no partner assigned yet — unable to fully evaluate"),
        (_noRatingCheckColor, "Player DUPR rating not found")
    };

            foreach (var (color, description) in colorKeys)
            {
                // Color swatch cell
                var swatchCell = summary.Cell(currentRow, 1);
                swatchCell.Style.Fill.BackgroundColor = color;
                swatchCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                swatchCell.Style.Border.OutsideBorderColor = XLColor.FromArgb(68, 114, 196);

                // Description cell
                var descCell = summary.Cell(currentRow, 2);
                descCell.Value = description;
                descCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                descCell.Style.Font.FontSize = 11;

                currentRow++;
            }

            // Spacer between key and division list
            currentRow++;

            var grouped = eventInfo
                .GroupBy(e => $"{e.PlayerGroup} {e.Format}")
                .OrderBy(g => g.Key)
                .ToList();

            int divisionNumber = 1;

            foreach (var group in grouped)
            {
                var categoryCell = summary.Cell(currentRow, 1);
                categoryCell.Value = group.Key;
                categoryCell.Style.Font.Bold = true;
                categoryCell.Style.Font.FontSize = 12;
                categoryCell.Style.Font.FontColor = _passedCheckColor;
                categoryCell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
                categoryCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                summary.Range(currentRow, 1, currentRow, 2).Merge();
                currentRow++;

                summary.Cell(currentRow, 1).Value = "#";
                summary.Cell(currentRow, 2).Value = "Division";

                foreach (var cell in summary.Range(currentRow, 1, currentRow, 2).Cells())
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromArgb(31, 73, 125);
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(189, 215, 238);
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;

                var ageGroups = group
                    .GroupBy(e => e.AgeGroup)
                    .OrderBy(g => g.Key)
                    .ToList();

                int categoryIndex = 0;
                foreach (var ageGroup in ageGroups)
                {
                    var sheetName = SanitizeSheetName($"{group.First().PlayerGroup} {group.First().Format} - {ageGroup.Key}");
                    if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var ws))
                        continue;

                    bool isEvenRow = categoryIndex % 2 == 0;

                    summary.Cell(currentRow, 1).Value = divisionNumber++;
                    summary.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var linkCell = summary.Cell(currentRow, 2);
                    linkCell.Value = $"{group.First().PlayerGroup} {group.First().Format} - {ageGroup.Key}";
                    linkCell.SetHyperlink(new XLHyperlink($"'{ws.Name}'!A1"));
                    linkCell.Style.Font.FontColor = XLColor.FromArgb(68, 114, 196);
                    linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;

                    if (isEvenRow)
                        summary.Range(currentRow, 1, currentRow, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(235, 241, 250);

                    currentRow++;
                    categoryIndex++;
                }

                currentRow++;
            }

            summary.Columns().AdjustToContents();
        }

        private static string SanitizeSheetName(string name)
        {
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
            return sanitized.Length > 31 ? sanitized[..31] : sanitized;
        }

        private static XLColor GetSinglesDuprCellColor(double playerSingles, double lower, double upper)
        {
            if (playerSingles == 0.0 && upper >= 4.0)
            {
                return _noRatingCheckColor;
            }

            if (playerSingles > upper || playerSingles < lower - 0.500)
                return _failedCheckColor;

            return _passedCheckColor;
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

            if (team.PlayerOne.DoublesDuprRating == 0.0 && team.PlayerTwo.DoublesDuprRating == 0.0)
                return upperSkillRating >= 4.0 ? _failedCheckColor : _passedCheckColor;

            if (playerUnrated || partnerUnrated)
                return GetUnratedColor(playerDoubles, partnerDoubles, lower, upper);

            if (playerDoubles > upper)
                return _failedCheckColor;

            if (playerDoubles < hardFloor)
                return _failedCheckColor;

            if (playerDoubles >= lower)
                return _passedCheckColor;

            bool partnerInRange = partnerDoubles >= lower && partnerDoubles <= upper;
            bool teamAvgAcceptable = team.AverageTeamDupr >= softFloor;

            return partnerInRange || teamAvgAcceptable ? _passedCheckColor : _failedCheckColor;
        }

        private static XLColor GetUnratedColor(double playerDoubles, double partnerDoubles, double lower, double upper)
        {
            if (upper > 4.0)
                return _failedCheckColor;

            double ratedPlayerDoubles = playerDoubles == 0.0 ? partnerDoubles : playerDoubles;
            bool ratedPartnerInRange = ratedPlayerDoubles >= lower && ratedPlayerDoubles <= upper;

            return ratedPartnerInRange ? _passedCheckColor : _failedCheckColor;
        }
    }
}