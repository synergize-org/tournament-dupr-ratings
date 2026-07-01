using ClosedXML.Excel;

namespace TournamentDuprRatings.Constants.Excel
{
    /// <summary>Named colors used for cell fills/fonts, so their meaning is clear at each call site.</summary>
    public static class ExcelPalette
    {
        public static readonly XLColor PassedCheck = XLColor.White;
        public static readonly XLColor FailedCheck = XLColor.Salmon;
        public static readonly XLColor NoPartnerCheck = XLColor.Flavescent;
        public static readonly XLColor NoRatingCheck = XLColor.DarkOrchid;

        /// <summary>Text color used on dark title/header fills. Coincidentally the same value as
        /// <see cref="PassedCheck"/>, but represents a different concept (readable text, not a status).</summary>
        public static readonly XLColor HeaderText = XLColor.White;

        public static readonly XLColor TitleBackground = XLColor.FromArgb(31, 73, 125);
        public static readonly XLColor AccentBlue = XLColor.FromArgb(68, 114, 196);
        public static readonly XLColor EvenRowBackground = XLColor.FromArgb(235, 241, 250);
        public static readonly XLColor TableHeaderBackground = XLColor.FromArgb(189, 215, 238);

        /// <summary>Muted text color for low-priority/internal data (e.g. the internal id column)
        /// that should stay visible - per Excel's data guidelines - without drawing attention.</summary>
        public static readonly XLColor MutedText = XLColor.FromArgb(150, 150, 150);
    }
}
