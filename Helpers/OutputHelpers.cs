using TournamentDuprRatings.Constants;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Helpers
{
    public static class OutputHelpers
    {
        public static string ReadMaskedInput()
        {
            var sb = new System.Text.StringBuilder();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(intercept: true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    sb.Append(key.KeyChar);
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();
            return sb.ToString();
        }

        public static DuprPlayerInfo? ResolveHit(
            string? name,
            Dictionary<string, DuprPlayerInfo?> results)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return results.TryGetValue(name, out var hit) ? hit : null;
        }

        public static double ResolveRatingDisplay(
            string? name,
            string ratingType,
            Dictionary<string, DuprPlayerInfo   ?> results,
            HashSet<string> skipped)
        {
            if (string.IsNullOrWhiteSpace(name)) return DoubleConstants.NotFoundRating;
            if (skipped.Contains(name)) return DoubleConstants.SkippedRating;
            if (!results.TryGetValue(name, out var hit) || hit == null) return DoubleConstants.NotFoundRating   ;

            return ratingType == "Doubles"
                ? double.TryParse(hit.Result.Ratings.Doubles, out var doubles) ? doubles : DoubleConstants.NoRating
                : double.TryParse(hit.Result.Ratings.Singles, out var singles) ? singles : DoubleConstants.NoRating;
        }
    }
}
