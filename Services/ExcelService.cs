using ClosedXML.Excel;
using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Constants.Excel;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services
{
    /// <summary>
    /// Builds the tournament results workbook: one worksheet per division (grouped by
    /// player group / format / age group) plus a "Summary" sheet that links to every division
    /// and explains the DUPR rating color-coding used throughout.
    /// </summary>
    public class ExcelService
    {
        private const string SinglesFormatName = "Singles";
        private const string SummarySheetName = "Summary";
        private const int SummarySheetPosition = 1;
        private const int SummaryColumnCount = 2;
        private const int RowsBetweenDivisions = 2;
        private const int PlaceColumnWidth = 8;
        private const int MaxExcelSheetNameLength = 31;
        private const string RatingNumberFormat = "0.000";
        private const string NoDuprIdLabel = "-";
        private const string DuprDashboardPlayerUrlPrefix = "https://dashboard.dupr.com/dashboard/player/";

        public static void GenerateEventResultsExcel(List<EventInfo> eventInfo, string fileName)
        {
            var filePath = BuildOutputFilePath(fileName);

            using var workbook = new XLWorkbook();

            foreach (var sheetGroup in GroupEventsByDivisionSheet(eventInfo))
            {
                WriteDivisionSheet(workbook, sheetGroup);
            }

            UpdateSummarySheet(workbook, eventInfo);
            workbook.SaveAs(filePath);
        }

        /// <summary>Builds the timestamped output path, creating the destination directory if needed.</summary>
        private static string BuildOutputFilePath(string fileName)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var outputDir = Environment.GetEnvironmentVariable("REPORT_OUTPUT_PATH")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Directory.CreateDirectory(outputDir);
            return Path.Combine(outputDir, $"{fileName}_{timestamp}.xlsx");
        }

        /// <summary>Groups events into one worksheet per player group / format / age group combination.</summary>
        private static List<IGrouping<string, EventInfo>> GroupEventsByDivisionSheet(List<EventInfo> eventInfo) =>
            eventInfo.GroupBy(e => $"{e.PlayerGroup} {e.Format} - {e.AgeGroup}").ToList();

        /// <summary>Writes every division section belonging to one worksheet, then auto-sizes its columns.</summary>
        private static void WriteDivisionSheet(XLWorkbook workbook, IGrouping<string, EventInfo> sheetGroup)
        {
            var sheet = workbook.Worksheets.Add(SanitizeSheetName(sheetGroup.Key));
            sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            bool isSingles = sheetGroup.FirstOrDefault()?.Format?.Equals(SinglesFormatName, StringComparison.OrdinalIgnoreCase) ?? false;

            int currentRow = 1;
            foreach (var divisionEvent in sheetGroup)
            {
                currentRow = WriteEventSection(sheet, divisionEvent, currentRow, isSingles) + RowsBetweenDivisions;
            }

            sheet.Columns().AdjustToContents();
            sheet.Column(ExcelColumns.Place).Width = PlaceColumnWidth;
            sheet.Column(ExcelColumns.Place + 1).Width = PlaceColumnWidth;
        }

        /// <summary>Writes one division's title, column headers, and team rows. Returns the last row written.</summary>
        private static int WriteEventSection(IXLWorksheet sheet, EventInfo eventInfo, int startRow, bool isSingles)
        {
            int visibleColumnCount = isSingles ? ExcelColumns.Singles.VisibleColumnCount : ExcelColumns.Doubles.VisibleColumnCount;

            int row = WriteSectionTitle(sheet, eventInfo.EventTitle, startRow, visibleColumnCount);
            row = WriteColumnHeaders(sheet, row, isSingles);

            int place = 1;
            foreach (var team in eventInfo.Teams)
            {
                bool isEvenRow = place % 2 == 0;
                if (isSingles)
                    WriteSinglesRow(sheet, row, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
                else
                    WriteDoublesRow(sheet, row, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);

                sheet.Cell(row, ExcelColumns.Place).Value = place++;
                MergeColumnSpan(sheet, row, ExcelColumns.Place);

                row++;
            }

            return row - 1;
        }

        private static int WriteSectionTitle(IXLWorksheet sheet, string? title, int row, int visibleColumnCount)
        {
            var titleCell = sheet.Cell(row, 1);
            titleCell.Value = title;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = ExcelFontSizes.SectionTitle;
            titleCell.Style.Font.FontColor = ExcelPalette.HeaderText;
            titleCell.Style.Fill.BackgroundColor = ExcelPalette.TitleBackground;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(row, 1, row, visibleColumnCount).Merge();

            return row + 1;
        }

        private static int WriteColumnHeaders(IXLWorksheet sheet, int row, bool isSingles)
        {
            string[] headers = isSingles
                ? ["Place", "Player Name", "DUPR ID", "Singles DUPR", "On Waitlist"]
                : ["Place", "Player 1 Name", "Player 1 DUPR ID", "Player 1 Doubles", "Player 2 Name", "Player 2 DUPR ID", "Player 2 Doubles", "Average Team DUPR", "On Waitlist"];

            for (int i = 0; i < headers.Length; i++)
            {
                int col = (i * ExcelColumns.ColumnSpan) + 1;
                var cell = sheet.Cell(row, col);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = ExcelPalette.HeaderText;
                cell.Style.Fill.BackgroundColor = ExcelPalette.AccentBlue;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                MergeColumnSpan(sheet, row, col);
            }

            return row + 1;
        }

        private static void WriteSinglesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            ApplyRowShading(sheet, row, ExcelColumns.Singles.VisibleColumnCount, isEvenRow);

            WritePlayerNameCell(sheet, row, ExcelColumns.Singles.PlayerName, team.PlayerOne);
            WriteDuprIdCell(sheet, row, ExcelColumns.Singles.PlayerDuprId, team.PlayerOne);

            double rating = team.PlayerOne?.SinglesDuprRating ?? DoubleConstants.NoRating;
            var ratingCell = sheet.Cell(row, ExcelColumns.Singles.PlayerRating);
            ratingCell.Value = rating;
            ratingCell.Style.NumberFormat.Format = RatingNumberFormat;
            ratingCell.Style.Fill.BackgroundColor = GetSinglesDuprCellColor(rating, lowerSkillRating, upperSkillRating);
            MergeColumnSpan(sheet, row, ExcelColumns.Singles.PlayerRating);

            WriteWaitlistCell(sheet, row, ExcelColumns.Singles.OnWaitlist, team.IsOnWaitList);
            WriteHiddenUniqueIdCell(sheet, row, ExcelColumns.Singles.UniqueId, team.UniqueId);
        }

        private static void WriteDoublesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            bool missingPartner = string.IsNullOrEmpty(team.PlayerTwo?.FullName?.Trim());

            ApplyRowShading(sheet, row, ExcelColumns.Doubles.VisibleColumnCount, isEvenRow);

            WritePlayerNameCell(sheet, row, ExcelColumns.Doubles.Player1Name, team.PlayerOne);
            WriteDuprIdCell(sheet, row, ExcelColumns.Doubles.Player1DuprId, team.PlayerOne);
            WriteDoublesRatingCell(sheet, row, ExcelColumns.Doubles.Player1Rating, team, isPlayer1: true, missingPartner, lowerSkillRating, upperSkillRating);

            WritePlayerNameCell(sheet, row, ExcelColumns.Doubles.Player2Name, team.PlayerTwo);
            WriteDuprIdCell(sheet, row, ExcelColumns.Doubles.Player2DuprId, team.PlayerTwo);
            WriteDoublesRatingCell(sheet, row, ExcelColumns.Doubles.Player2Rating, team, isPlayer1: false, missingPartner, lowerSkillRating, upperSkillRating);

            var avgCell = sheet.Cell(row, ExcelColumns.Doubles.AverageTeamDupr);
            avgCell.Value = team.AverageTeamDupr;
            avgCell.Style.NumberFormat.Format = RatingNumberFormat;
            MergeColumnSpan(sheet, row, ExcelColumns.Doubles.AverageTeamDupr);

            WriteWaitlistCell(sheet, row, ExcelColumns.Doubles.OnWaitlist, team.IsOnWaitList);
            WriteHiddenUniqueIdCell(sheet, row, ExcelColumns.Doubles.UniqueId, team.UniqueId);
        }

        /// <summary>Shades the full row for readability, alternating between white and light blue.</summary>
        private static void ApplyRowShading(IXLWorksheet sheet, int row, int visibleColumnCount, bool isEvenRow)
        {
            sheet.Range(row, 1, row, visibleColumnCount).Style.Fill.BackgroundColor = isEvenRow
                ? ExcelPalette.EvenRowBackground
                : XLColor.NoColor;
        }

        /// <summary>Writes a player's display name, linking to their pickleball.com profile when available.</summary>
        private static void WritePlayerNameCell(IXLWorksheet sheet, int row, int col, PlayerInfo? player)
        {
            var cell = sheet.Cell(row, col);
            cell.Value = SanitizeCellText(player?.FullName ?? "");
            if (!string.IsNullOrEmpty(player?.PbbLink))
                cell.SetHyperlink(new XLHyperlink(player.PbbLink));
            MergeColumnSpan(sheet, row, col);
        }

        /// <summary>Writes a player's DUPR id, linking to their DUPR dashboard profile when available.</summary>
        private static void WriteDuprIdCell(IXLWorksheet sheet, int row, int col, PlayerInfo? player)
        {
            var cell = sheet.Cell(row, col);
            cell.Value = player?.DuprId ?? NoDuprIdLabel;
            if (!string.IsNullOrEmpty(player?.DuprId))
                cell.SetHyperlink(new XLHyperlink($"{DuprDashboardPlayerUrlPrefix}{player?.Id}"));
            MergeColumnSpan(sheet, row, col);
        }

        /// <summary>
        /// Writes one player's doubles DUPR rating cell, colored according to division requirements
        /// (or flagged as "no partner" when the team roster is incomplete).
        /// </summary>
        private static void WriteDoublesRatingCell(IXLWorksheet sheet, int row, int col, TeamInfo team, bool isPlayer1, bool missingPartner, double lowerSkillRating, double upperSkillRating)
        {
            var player = isPlayer1 ? team.PlayerOne : team.PlayerTwo;
            double rating = player?.DoublesDuprRating ?? DoubleConstants.NoRating;

            var color = missingPartner
                ? ExcelPalette.NoPartnerCheck
                : GetDoublesDuprCellColor(rating, team, isPlayer1, lowerSkillRating, upperSkillRating);
            color = ApplyRatingNotFoundOverride(color, player?.DoublesDuprRating);

            var cell = sheet.Cell(row, col);
            cell.Value = rating;
            cell.Style.NumberFormat.Format = RatingNumberFormat;
            cell.Style.Fill.BackgroundColor = color;
            MergeColumnSpan(sheet, row, col);
        }

        private static void WriteWaitlistCell(IXLWorksheet sheet, int row, int col, bool isOnWaitlist)
        {
            sheet.Cell(row, col).Value = isOnWaitlist ? "Yes" : "No";
            MergeColumnSpan(sheet, row, col);
        }

        /// <summary>Writes the team's unique id into a hidden column, used for future lookups/debugging.</summary>
        private static void WriteHiddenUniqueIdCell(IXLWorksheet sheet, int row, int col, string uniqueId)
        {
            var idCell = sheet.Cell(row, col);
            idCell.Value = uniqueId;
            idCell.Style.Fill.BackgroundColor = XLColor.NoColor;
            sheet.Column(col).Hide();
        }

        /// <summary>Merges a field's 2-column span (starting at <paramref name="col"/>) into one visual cell.</summary>
        private static void MergeColumnSpan(IXLWorksheet sheet, int row, int col) =>
            sheet.Range(row, col, row, col + ExcelColumns.ColumnSpan - 1).Merge();

        /// <summary>
        /// Defensive override: if the computed color is "passed" for a player whose DUPR lookup
        /// failed entirely (as opposed to simply being unrated), force the "rating not found" color instead.
        /// </summary>
        private static XLColor ApplyRatingNotFoundOverride(XLColor computedColor, double? rating) =>
            computedColor == ExcelPalette.PassedCheck && rating == DoubleConstants.NotFoundRating
                ? ExcelPalette.NoRatingCheck
                : computedColor;

        private static readonly (XLColor Color, string Description)[] _colorKeyEntries =
        [
            (ExcelPalette.PassedCheck, "Player DUPR is within the required range"),
            (ExcelPalette.FailedCheck, "Player DUPR does not meet division requirements"),
            (ExcelPalette.NoPartnerCheck, "Player has no partner assigned yet — unable to fully evaluate"),
            (ExcelPalette.NoRatingCheck, "Player DUPR rating not found"),
        ];

        private static void UpdateSummarySheet(XLWorkbook workbook, List<EventInfo> eventInfo)
        {
            var summary = workbook.Worksheets.Add(SummarySheetName);
            workbook.Worksheets.Worksheet(SummarySheetName).Position = SummarySheetPosition;

            int currentRow = WriteSummaryTitle(summary, 1);
            currentRow = WriteColorKeySection(summary, currentRow);
            currentRow++; // Spacer between the color key and the division list

            WriteDivisionGroups(workbook, summary, eventInfo, currentRow);

            summary.Columns().AdjustToContents();
        }

        private static int WriteSummaryTitle(IXLWorksheet summary, int row)
        {
            var titleCell = summary.Cell(row, 1);
            titleCell.Value = "Event Summary";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = ExcelFontSizes.SummaryTitle;
            titleCell.Style.Font.FontColor = ExcelPalette.HeaderText;
            titleCell.Style.Fill.BackgroundColor = ExcelPalette.TitleBackground;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            summary.Range(row, 1, row, SummaryColumnCount).Merge();

            return row + 1;
        }

        private static int WriteColorKeySection(IXLWorksheet summary, int row)
        {
            var keyHeaderCell = summary.Cell(row, 1);
            keyHeaderCell.Value = "Color Key";
            keyHeaderCell.Style.Font.Bold = true;
            keyHeaderCell.Style.Font.FontSize = ExcelFontSizes.SectionHeader;
            keyHeaderCell.Style.Font.FontColor = ExcelPalette.HeaderText;
            keyHeaderCell.Style.Fill.BackgroundColor = ExcelPalette.AccentBlue;
            keyHeaderCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            summary.Range(row, 1, row, SummaryColumnCount).Merge();
            row++;

            foreach (var (color, description) in _colorKeyEntries)
            {
                var swatchCell = summary.Cell(row, 1);
                swatchCell.Style.Fill.BackgroundColor = color;
                swatchCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                swatchCell.Style.Border.OutsideBorderColor = ExcelPalette.AccentBlue;

                var descCell = summary.Cell(row, 2);
                descCell.Value = description;
                descCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                descCell.Style.Font.FontSize = ExcelFontSizes.Description;

                row++;
            }

            return row;
        }

        /// <summary>Writes each player-group/format category, its divisions (grouped by age group), and
        /// links to the matching worksheet.</summary>
        private static void WriteDivisionGroups(XLWorkbook workbook, IXLWorksheet summary, List<EventInfo> eventInfo, int startRow)
        {
            var groupedByCategory = eventInfo
                .GroupBy(e => $"{e.PlayerGroup} {e.Format}")
                .OrderBy(g => g.Key)
                .ToList();

            int currentRow = startRow;
            int divisionNumber = 1;

            foreach (var categoryGroup in groupedByCategory)
            {
                currentRow = WriteDivisionCategoryHeader(summary, categoryGroup.Key, currentRow);

                var ageGroups = categoryGroup
                    .GroupBy(e => e.AgeGroup)
                    .OrderBy(g => g.Key)
                    .ToList();

                int ageGroupIndex = 0;
                foreach (var ageGroup in ageGroups)
                {
                    var sheetName = SanitizeSheetName($"{categoryGroup.First().PlayerGroup} {categoryGroup.First().Format} - {ageGroup.Key}");
                    if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
                        continue;

                    bool isEvenRow = ageGroupIndex % 2 == 0;
                    WriteDivisionLinkRow(summary, currentRow, divisionNumber++, categoryGroup.First(), ageGroup.Key, worksheet, isEvenRow);

                    currentRow++;
                    ageGroupIndex++;
                }

                currentRow++;
            }
        }

        private static int WriteDivisionCategoryHeader(IXLWorksheet summary, string categoryName, int row)
        {
            var categoryCell = summary.Cell(row, 1);
            categoryCell.Value = categoryName;
            categoryCell.Style.Font.Bold = true;
            categoryCell.Style.Font.FontSize = ExcelFontSizes.SectionHeader;
            categoryCell.Style.Font.FontColor = ExcelPalette.HeaderText;
            categoryCell.Style.Fill.BackgroundColor = ExcelPalette.AccentBlue;
            categoryCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            summary.Range(row, 1, row, SummaryColumnCount).Merge();
            row++;

            summary.Cell(row, 1).Value = "#";
            summary.Cell(row, 2).Value = "Division";
            foreach (var cell in summary.Range(row, 1, row, SummaryColumnCount).Cells())
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = ExcelPalette.TitleBackground;
                cell.Style.Fill.BackgroundColor = ExcelPalette.TableHeaderBackground;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            return row + 1;
        }

        private static void WriteDivisionLinkRow(IXLWorksheet summary, int row, int divisionNumber, EventInfo sampleEvent, string? ageGroup, IXLWorksheet targetWorksheet, bool isEvenRow)
        {
            summary.Cell(row, 1).Value = divisionNumber;
            summary.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var linkCell = summary.Cell(row, 2);
            linkCell.Value = $"{sampleEvent.PlayerGroup} {sampleEvent.Format} - {ageGroup}";
            linkCell.SetHyperlink(new XLHyperlink($"'{targetWorksheet.Name}'!A1"));
            linkCell.Style.Font.FontColor = ExcelPalette.AccentBlue;
            linkCell.Style.Font.Underline = XLFontUnderlineValues.Single;

            if (isEvenRow)
                summary.Range(row, 1, row, SummaryColumnCount).Style.Fill.BackgroundColor = ExcelPalette.EvenRowBackground;
        }

        private static readonly char[] _invalidSheetNameChars = [':', '\\', '/', '?', '*', '[', ']'];

        private static string SanitizeSheetName(string name)
        {
            var sanitized = string.Concat(name.Select(c => _invalidSheetNameChars.Contains(c) ? '_' : c)).Trim();
            return sanitized.Length > MaxExcelSheetNameLength ? sanitized[..MaxExcelSheetNameLength] : sanitized;
        }

        // Player names originate from third-party registration data (Pickleball Tournaments / DUPR)
        // and are not trusted. Prefix values that would otherwise be interpreted as a formula
        // (starting with =, +, -, or @) so Excel treats them as plain text instead of executing them.
        private static readonly char[] _formulaTriggerChars = ['=', '+', '-', '@'];

        private static string SanitizeCellText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return _formulaTriggerChars.Contains(value[0]) ? "'" + value : value;
        }

        private static XLColor GetSinglesDuprCellColor(double playerRating, double lowerSkillRating, double upperSkillRating)
        {
            bool isUnrated = playerRating == DoubleConstants.NoRating;
            bool isOpenDivision = upperSkillRating >= ExcelRatingThresholds.OpenDivisionRatingThreshold;

            if (isUnrated && isOpenDivision)
                return ExcelPalette.NoRatingCheck;

            double hardFloor = lowerSkillRating - ExcelRatingThresholds.HardFloorMargin;
            if (playerRating > upperSkillRating || playerRating < hardFloor)
                return ExcelPalette.FailedCheck;

            return ExcelPalette.PassedCheck;
        }

        private static XLColor GetDoublesDuprCellColor(double playerRating, TeamInfo team, bool isPlayer1, double lowerSkillRating, double upperSkillRating)
        {
            double hardFloor = lowerSkillRating - ExcelRatingThresholds.HardFloorMargin;
            double softFloor = lowerSkillRating - ExcelRatingThresholds.SoftFloorMargin;

            double partnerRating = isPlayer1
                ? team?.PlayerTwo?.DoublesDuprRating ?? DoubleConstants.NoRating
                : team?.PlayerOne?.DoublesDuprRating ?? DoubleConstants.NoRating;

            bool playerUnrated = playerRating == DoubleConstants.NoRating;
            bool partnerUnrated = partnerRating == DoubleConstants.NoRating;
            bool bothUnrated = team?.PlayerOne?.DoublesDuprRating == DoubleConstants.NoRating
                && team?.PlayerTwo?.DoublesDuprRating == DoubleConstants.NoRating;

            if (bothUnrated)
                return upperSkillRating >= ExcelRatingThresholds.OpenDivisionRatingThreshold ? ExcelPalette.FailedCheck : ExcelPalette.PassedCheck;

            if (playerUnrated || partnerUnrated)
                return GetUnratedTeamMemberColor(playerRating, partnerRating, lowerSkillRating, upperSkillRating);

            if (playerRating > upperSkillRating || playerRating < hardFloor)
                return ExcelPalette.FailedCheck;

            if (playerRating >= lowerSkillRating)
                return ExcelPalette.PassedCheck;

            bool partnerInRange = partnerRating >= lowerSkillRating && partnerRating <= upperSkillRating;
            bool teamAverageAcceptable = team?.AverageTeamDupr >= softFloor;

            return partnerInRange || teamAverageAcceptable ? ExcelPalette.PassedCheck : ExcelPalette.FailedCheck;
        }

        /// <summary>
        /// Colors a team member when either they or their partner has no DUPR rating yet. In "Open"
        /// divisions an unrated player always fails; otherwise the rated player (if any) must fall
        /// within the division's range for the pairing to pass.
        /// </summary>
        private static XLColor GetUnratedTeamMemberColor(double playerRating, double partnerRating, double lowerSkillRating, double upperSkillRating)
        {
            if (upperSkillRating > ExcelRatingThresholds.OpenDivisionRatingThreshold)
                return ExcelPalette.FailedCheck;

            double ratedPlayerRating = playerRating == DoubleConstants.NoRating ? partnerRating : playerRating;
            bool ratedPlayerInRange = ratedPlayerRating >= lowerSkillRating && ratedPlayerRating <= upperSkillRating;

            return ratedPlayerInRange ? ExcelPalette.PassedCheck : ExcelPalette.FailedCheck;
        }
    }
}