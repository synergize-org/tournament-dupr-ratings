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

        public static DuprPlayerHit? ResolveHit(
            string? name,
            Dictionary<string, DuprPlayerHit?> results)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return results.TryGetValue(name, out var hit) ? hit : null;
        }

        public static string ResolveRatingDisplay(
            string? name,
            string ratingType,
            Dictionary<string, DuprPlayerHit?> results,
            HashSet<string> skipped)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Not Found";
            if (skipped.Contains(name)) return "Skipped";
            if (!results.TryGetValue(name, out var hit) || hit == null) return "Not Found";

            return ratingType == "Doubles"
                ? hit.Ratings?.Doubles ?? ""
                : hit.Ratings?.Singles ?? "";
        }
    }
}
