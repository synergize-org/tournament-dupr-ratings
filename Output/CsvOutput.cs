using System.Text;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Output;

public static class CsvOutput
{
    public static void WriteFile(List<TeamResult> teams, string activityId)
    {
        var filename = $"tournament-{activityId}-ratings.csv";
        var sb = new StringBuilder();

        sb.AppendLine("Team,Player1,Player1 DUPR ID,Player1 Doubles,Player1 Singles," +
                      "Player2,Player2 DUPR ID,Player2 Doubles,Player2 Singles");

        for (int i = 0; i < teams.Count; i++)
        {
            var t = teams[i];
            sb.AppendLine(string.Join(',',
            [
                (i + 1).ToString(),
                Escape(t.Player1Name), Escape(t.Player1DuprId), Escape(t.Player1Doubles), Escape(t.Player1Singles),
                Escape(t.Player2Name), Escape(t.Player2DuprId), Escape(t.Player2Doubles), Escape(t.Player2Singles)
            ]));
        }

        File.WriteAllText(filename, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Results saved to: {Path.GetFullPath(filename)}");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
