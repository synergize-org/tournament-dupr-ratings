using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Output;

public static class ConsoleOutput
{
    public static void PrintTable(List<EventResults> teams)
    {
        foreach (var team in teams)
        {
            Console.WriteLine();
            Console.WriteLine($"Event: {team.Title}");
            Console.WriteLine(
                $"{"#",-4} {"Player 1",-26} {"P1 DUPR ID",-12} {"P1 Dbl",-8} {"P1 Sgl",-8} " +
                $"{"Player 2",-26} {"P2 DUPR ID",-12} {"P2 Dbl",-8} {"P2 Sgl"}");
            Console.WriteLine(new string('-', 122));

            for (int i = 0; i < team.TeamResults.Count; i++)
            {
                var t = team.TeamResults[i];
                Console.WriteLine(
                    $"{i + 1,-4} {t.Player1Name,-26} {t.Player1DuprId ?? "",-12} {t.Player1Doubles,-8} {t.Player1Singles,-8} " +
                    $"{t.Player2Name,-26} {t.Player2DuprId ?? "",-12} {t.Player2Doubles,-8} {t.Player2Singles}");
            }

            Console.WriteLine();
        }
    }
}
