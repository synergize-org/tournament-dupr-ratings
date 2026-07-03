using ClosedXML.Excel;
using System.Globalization;
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

        private const double MinDataColumnWidth = 10;
        private const double MaxDataColumnWidth = 30;

        private const int MaxExcelSheetNameLength = 31;
        private const string RatingNumberFormat = "0.000";
        private const string NoDuprIdLabel = "-";
        private const string InternalIdHeader = "Internal ID";
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
            int totalColumnCount = isSingles ? ExcelColumns.Singles.TotalColumnCount : ExcelColumns.Doubles.TotalColumnCount;

            int currentRow = 1;
            foreach (var divisionEvent in sheetGroup)
            {
                currentRow = WriteEventSection(sheet, divisionEvent, currentRow, isSingles) + RowsBetweenDivisions;
            }

            AutoFitColumns(sheet, totalColumnCount);
        }

        /// <summary>
        /// Auto-sizes every column to its content (each field occupies exactly one column, so this
        /// measures real values instead of guessing at a merged range), then clamps data columns to a
        /// readable width range so no name is clipped and no single column dominates the sheet.
        /// </summary>
        private static void AutoFitColumns(IXLWorksheet sheet, int totalColumnCount)
        {
            sheet.Columns(1, totalColumnCount).AdjustToContents();

            for (int col = ExcelColumns.Place + 1; col <= totalColumnCount; col++)
            {
                var column = sheet.Column(col);
                column.Width = Math.Clamp(column.Width, MinDataColumnWidth, MaxDataColumnWidth);
            }

            sheet.Column(ExcelColumns.Place).Width = PlaceColumnWidth;
        }

        /// <summary>Writes one division's title, column headers, and team rows. Returns the last row written.</summary>
        private static int WriteEventSection(IXLWorksheet sheet, EventInfo eventInfo, int startRow, bool isSingles)
        {
            int totalColumnCount = isSingles ? ExcelColumns.Singles.TotalColumnCount : ExcelColumns.Doubles.TotalColumnCount;

            int row = WriteSectionTitle(sheet, eventInfo.EventTitle, startRow, totalColumnCount);
            row = WriteColumnHeaders(sheet, row, isSingles);

            int firstDataRow = row;
            int place = 1;
            foreach (var team in eventInfo.Teams)
            {
                bool isEvenRow = place % 2 == 0;
                if (isSingles)
                    WriteSinglesRow(sheet, row, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
                else
                    WriteDoublesRow(sheet, row, team, isEvenRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);

                sheet.Cell(row, ExcelColumns.Place).Value = place++;

                row++;
            }

            int lastDataRow = row - 1;
            if (isSingles)
                ApplySinglesRatingConditionalFormat(sheet, firstDataRow, lastDataRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);
            else
                ApplyDoublesRatingConditionalFormat(sheet, firstDataRow, lastDataRow, eventInfo.SkillGroup.lower, eventInfo.SkillGroup.upper);

            return lastDataRow;
        }

        private static int WriteSectionTitle(IXLWorksheet sheet, string? title, int row, int totalColumnCount)
        {
            var titleCell = sheet.Cell(row, 1);
            titleCell.Value = title;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = ExcelFontSizes.SectionTitle;
            titleCell.Style.Font.FontColor = ExcelPalette.HeaderText;
            titleCell.Style.Fill.BackgroundColor = ExcelPalette.TitleBackground;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range(row, 1, row, totalColumnCount).Merge();

            return row + 1;
        }

        private static int WriteColumnHeaders(IXLWorksheet sheet, int row, bool isSingles)
        {
            string[] headers = isSingles
                ? ["Place", "Player Name", "DUPR ID", "Singles DUPR", "On Waitlist", InternalIdHeader]
                : ["Place", "Player 1 Name", "Player 1 DUPR ID", "Player 1 Doubles", "Player 2 Name", "Player 2 DUPR ID", "Player 2 Doubles", "Average Team DUPR", "On Waitlist", InternalIdHeader];

            for (int col = 1; col <= headers.Length; col++)
            {
                var cell = sheet.Cell(row, col);
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = ExcelPalette.HeaderText;
                cell.Style.Fill.BackgroundColor = ExcelPalette.AccentBlue;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            return row + 1;
        }

        private static void WriteSinglesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            ApplyRowShading(sheet, row, ExcelColumns.Singles.TotalColumnCount, isEvenRow);

            WritePlayerNameCell(sheet, row, ExcelColumns.Singles.PlayerName, team.PlayerOne);
            WriteDuprIdCell(sheet, row, ExcelColumns.Singles.PlayerDuprId, team.PlayerOne);

            double rating = team.PlayerOne?.SinglesDuprRating ?? DoubleConstants.NoRating;
            var ratingCell = sheet.Cell(row, ExcelColumns.Singles.PlayerRating);
            ratingCell.Value = rating;
            ratingCell.Style.NumberFormat.Format = RatingNumberFormat;
            ratingCell.Style.Fill.BackgroundColor = GetSinglesDuprCellColor(rating, lowerSkillRating, upperSkillRating);

            WriteWaitlistCell(sheet, row, ExcelColumns.Singles.OnWaitlist, team.IsOnWaitList);
            WriteInternalIdCell(sheet, row, ExcelColumns.Singles.InternalId, team.UniqueId);
        }

        private static void WriteDoublesRow(IXLWorksheet sheet, int row, TeamInfo team, bool isEvenRow, double lowerSkillRating, double upperSkillRating)
        {
            bool missingPartner = string.IsNullOrEmpty(team.PlayerTwo?.FullName?.Trim());

            ApplyRowShading(sheet, row, ExcelColumns.Doubles.TotalColumnCount, isEvenRow);

            WritePlayerNameCell(sheet, row, ExcelColumns.Doubles.Player1Name, team.PlayerOne);
            WriteDuprIdCell(sheet, row, ExcelColumns.Doubles.Player1DuprId, team.PlayerOne);
            WriteDoublesRatingCell(sheet, row, ExcelColumns.Doubles.Player1Rating, team, isPlayer1: true, missingPartner, lowerSkillRating, upperSkillRating);

            WritePlayerNameCell(sheet, row, ExcelColumns.Doubles.Player2Name, team.PlayerTwo);
            WriteDuprIdCell(sheet, row, ExcelColumns.Doubles.Player2DuprId, team.PlayerTwo);
            WriteDoublesRatingCell(sheet, row, ExcelColumns.Doubles.Player2Rating, team, isPlayer1: false, missingPartner, lowerSkillRating, upperSkillRating);

            var avgCell = sheet.Cell(row, ExcelColumns.Doubles.AverageTeamDupr);
            avgCell.FormulaA1 = $"=({CellRef(sheet, row, ExcelColumns.Doubles.Player1Rating)}+{CellRef(sheet, row, ExcelColumns.Doubles.Player2Rating)})/2";
            avgCell.Style.NumberFormat.Format = RatingNumberFormat;

            WriteWaitlistCell(sheet, row, ExcelColumns.Doubles.OnWaitlist, team.IsOnWaitList);
            WriteInternalIdCell(sheet, row, ExcelColumns.Doubles.InternalId, team.UniqueId);
        }

        /// <summary>Shades the full row for readability, alternating between white and light blue.</summary>
        private static void ApplyRowShading(IXLWorksheet sheet, int row, int totalColumnCount, bool isEvenRow)
        {
            sheet.Range(row, 1, row, totalColumnCount).Style.Fill.BackgroundColor = isEvenRow
                ? ExcelPalette.EvenRowBackground
                : XLColor.NoColor;
        }

        /// <summary>Writes a player's display name, linking to their pickleball.com profile when available.</summary>
        private static void WritePlayerNameCell(IXLWorksheet sheet, int row, int col, PlayerInfo? player)
        {
            var cell = sheet.Cell(row, col);
            cell.Value = SanitizeCellText(player?.FullName?.Trim() ?? "");
            if (!string.IsNullOrEmpty(player?.PbbLink))
                cell.SetHyperlink(new XLHyperlink(player.PbbLink));
        }

        /// <summary>Writes a player's DUPR id, linking to their DUPR dashboard profile when available.</summary>
        private static void WriteDuprIdCell(IXLWorksheet sheet, int row, int col, PlayerInfo? player)
        {
            var cell = sheet.Cell(row, col);
            cell.Value = player?.DuprId ?? NoDuprIdLabel;
            if (!string.IsNullOrEmpty(player?.DuprId))
                cell.SetHyperlink(new XLHyperlink($"{DuprDashboardPlayerUrlPrefix}{player?.Id}"));
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
        }

        private static void WriteWaitlistCell(IXLWorksheet sheet, int row, int col, bool isOnWaitlist)
        {
            sheet.Cell(row, col).Value = isOnWaitlist ? "Yes" : "No";
        }

        /// <summary>
        /// Writes the team's internal lookup id. Kept visible (not hidden) per Excel's worksheet
        /// guidance - hiding columns within a data range risks accidental deletion and can prevent
        /// Excel from correctly detecting the range - but muted so it doesn't compete with the data.
        /// </summary>
        private static void WriteInternalIdCell(IXLWorksheet sheet, int row, int col, string internalId)
        {
            var idCell = sheet.Cell(row, col);
            idCell.Value = internalId;
            idCell.Style.Font.FontColor = ExcelPalette.MutedText;
        }

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

            int sectionsRow = WriteSummaryTitle(summary, 1);

            // Color Key (columns 1-2) and the requirements explanation (column 4, past an empty spacer
            // column 3) sit side by side starting on the same row, so the sheet grows wider, not taller.
            int colorKeyEndRow = WriteColorKeySection(summary, sectionsRow);

            WriteDivisionGroups(workbook, summary, eventInfo, colorKeyEndRow);

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

        // ---------------------------------------------------------------------------------------
        // Excel conditional formatting: mirrors GetSinglesDuprCellColor/GetDoublesDuprCellColor as
        // native Excel formulas, so the same colors are recomputed live if a user manually edits a
        // rating, waitlist, or player name cell after the workbook has been generated.
        // ---------------------------------------------------------------------------------------

        /// <summary>The A1-style address (e.g. "D5", no $ locks) of a cell, for building formula strings.</summary>
        private static string CellRef(IXLWorksheet sheet, int row, int col) =>
            sheet.Cell(row, col).Address.ToStringRelative();

        /// <summary>Formats a number using "." as the decimal separator, regardless of system locale.</summary>
        private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Adds one fill rule to a range: when <paramref name="formula"/> is true, apply <paramref name="color"/>
        /// and stop evaluating lower-priority rules (mirroring an if/else-if chain).</summary>
        private static void AddConditionalColorRule(IXLRange range, string formula, XLColor color)
        {
            var conditionalFormat = range.AddConditionalFormat();
            conditionalFormat.WhenIsTrue(formula).Fill.SetBackgroundColor(color);
            conditionalFormat.SetStopIfTrue();
        }

        /// <summary>Mirrors <see cref="GetSinglesDuprCellColor"/> as conditional formatting over one division's rating column.</summary>
        private static void ApplySinglesRatingConditionalFormat(IXLWorksheet sheet, int firstDataRow, int lastDataRow, double lowerSkillRating, double upperSkillRating)
        {
            if (firstDataRow > lastDataRow)
                return;

            int col = ExcelColumns.Singles.PlayerRating;
            var range = sheet.Range(firstDataRow, col, lastDataRow, col);
            string rating = CellRef(sheet, firstDataRow, col);

            bool isOpenDivision = upperSkillRating >= ExcelRatingThresholds.OpenDivisionRatingThreshold;
            double hardFloor = lowerSkillRating - ExcelRatingThresholds.HardFloorMargin;

            if (isOpenDivision)
                AddConditionalColorRule(range, $"{rating}={Num(DoubleConstants.NoRating)}", ExcelPalette.NoRatingCheck);

            AddConditionalColorRule(range, $"OR({rating}>{Num(upperSkillRating)},{rating}<{Num(hardFloor)})", ExcelPalette.FailedCheck);
            AddConditionalColorRule(range, "TRUE()", ExcelPalette.PassedCheck);
        }

        /// <summary>Mirrors <see cref="GetDoublesDuprCellColor"/> (plus the no-partner and rating-not-found
        /// handling from <see cref="WriteDoublesRatingCell"/>) as conditional formatting over both rating columns.</summary>
        private static void ApplyDoublesRatingConditionalFormat(IXLWorksheet sheet, int firstDataRow, int lastDataRow, double lowerSkillRating, double upperSkillRating)
        {
            if (firstDataRow > lastDataRow)
                return;

            string player1Rating = CellRef(sheet, firstDataRow, ExcelColumns.Doubles.Player1Rating);
            string player2Rating = CellRef(sheet, firstDataRow, ExcelColumns.Doubles.Player2Rating);
            string player2Name = CellRef(sheet, firstDataRow, ExcelColumns.Doubles.Player2Name);
            string averageTeamDupr = CellRef(sheet, firstDataRow, ExcelColumns.Doubles.AverageTeamDupr);

            var player1Range = sheet.Range(firstDataRow, ExcelColumns.Doubles.Player1Rating, lastDataRow, ExcelColumns.Doubles.Player1Rating);
            var player2Range = sheet.Range(firstDataRow, ExcelColumns.Doubles.Player2Rating, lastDataRow, ExcelColumns.Doubles.Player2Rating);

            ApplyDoublesRatingRules(player1Range, selfRef: player1Rating, partnerRef: player2Rating, player2NameRef: player2Name, averageRef: averageTeamDupr, lowerSkillRating, upperSkillRating);
            ApplyDoublesRatingRules(player2Range, selfRef: player2Rating, partnerRef: player1Rating, player2NameRef: player2Name, averageRef: averageTeamDupr, lowerSkillRating, upperSkillRating);
        }

        /// <summary>
        /// Writes the ordered fill rules for one rating column, in the same priority as the
        /// GetDoublesDuprCellColor/GetUnratedTeamMemberColor if/else-if chain (each rule stops evaluation
        /// once true, so priority order matters as much as the conditions themselves).
        /// </summary>
        private static void ApplyDoublesRatingRules(IXLRange range, string selfRef, string partnerRef, string player2NameRef, string averageRef, double lowerSkillRating, double upperSkillRating)
        {
            bool isOpenDivision = upperSkillRating >= ExcelRatingThresholds.OpenDivisionRatingThreshold;
            bool isAboveOpenThreshold = upperSkillRating > ExcelRatingThresholds.OpenDivisionRatingThreshold;
            double hardFloor = lowerSkillRating - ExcelRatingThresholds.HardFloorMargin;
            double softFloor = lowerSkillRating - ExcelRatingThresholds.SoftFloorMargin;

            string noRating = Num(DoubleConstants.NoRating);
            string notFoundRating = Num(DoubleConstants.NotFoundRating);
            string lower = Num(lowerSkillRating);
            string upper = Num(upperSkillRating);

            string eitherUnratedCondition = $"OR({selfRef}={noRating},{partnerRef}={noRating})";
            string ratedPlayerRatingExpr = $"IF({selfRef}={noRating},{partnerRef},{selfRef})";
            string ratedPlayerInRangeCondition = $"AND({ratedPlayerRatingExpr}>={lower},{ratedPlayerRatingExpr}<={upper})";
            string partnerInRangeCondition = $"AND({partnerRef}>={lower},{partnerRef}<={upper})";
            string teamAverageAcceptableCondition = $"{averageRef}>={Num(softFloor)}";

            // 1. Incomplete team (no second player registered) always shows as "no partner".
            AddConditionalColorRule(range, $"TRIM({player2NameRef})=\"\"", ExcelPalette.NoPartnerCheck);

            // 2. A rating that failed to look up entirely is always flagged (a simplification of the
            //    original code's narrower "would otherwise have passed" override - see ApplyRatingNotFoundOverride).
            AddConditionalColorRule(range, $"{selfRef}={notFoundRating}", ExcelPalette.NoRatingCheck);

            // 3. Neither player has a rating yet: fails in Open divisions, otherwise passes.
            AddConditionalColorRule(range, $"AND({selfRef}={noRating},{partnerRef}={noRating})", isOpenDivision ? ExcelPalette.FailedCheck : ExcelPalette.PassedCheck);

            // 4. Exactly one player is unrated: judged by whichever player IS rated (Open divisions always fail).
            if (isAboveOpenThreshold)
            {
                AddConditionalColorRule(range, eitherUnratedCondition, ExcelPalette.FailedCheck);
            }
            else
            {
                AddConditionalColorRule(range, $"AND({eitherUnratedCondition},{ratedPlayerInRangeCondition})", ExcelPalette.PassedCheck);
                AddConditionalColorRule(range, eitherUnratedCondition, ExcelPalette.FailedCheck);
            }

            // 5. Both players rated: hard-fail outside the division's floor/ceiling.
            AddConditionalColorRule(range, $"OR({selfRef}>{upper},{selfRef}<{Num(hardFloor)})", ExcelPalette.FailedCheck);

            // 6. Player meets the lower bound outright.
            AddConditionalColorRule(range, $"{selfRef}>={lower}", ExcelPalette.PassedCheck);

            // 7. Player is slightly under, but the partner (or team average) makes up for it.
            AddConditionalColorRule(range, $"OR({partnerInRangeCondition},{teamAverageAcceptableCondition})", ExcelPalette.PassedCheck);

            // 8. Otherwise, fails.
            AddConditionalColorRule(range, "TRUE()", ExcelPalette.FailedCheck);
        }

        internal static XLColor GetSinglesDuprCellColor(double playerRating, double lowerSkillRating, double upperSkillRating)
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

        internal static XLColor GetDoublesDuprCellColor(double playerRating, TeamInfo team, bool isPlayer1, double lowerSkillRating, double upperSkillRating)
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