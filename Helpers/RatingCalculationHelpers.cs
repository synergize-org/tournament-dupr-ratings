using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Helpers
{
    public static class RatingCalculationHelpers
    {
        public static (double lower, double upper) GetSkillGroup(string skillGroup)
        {
            Console.WriteLine($"Parsing skill group: {skillGroup}");

            var skillGroupLower = skillGroup.ToLower();
            if (skillGroupLower.Contains("to"))
            {
                var split = skillGroupLower.Split("to");
                return (double.Parse(split[0].Trim()), double.Parse(split[1].Trim()));
            }

            if (skillGroupLower.Contains("and under"))
            {
                var split = skillGroupLower.Split("and under");
                return (0.0, double.Parse(split[0].Trim()) + 0.5);
            }

            if (skillGroupLower.Contains("and above"))
            {
                var split = skillGroupLower.Split("and above");
                return (double.Parse(split[0].Trim()), 10.0);
            }

            var skillGroupParsed = double.TryParse(skillGroup, out var parsedValue);

            if (!skillGroupParsed)
            {
                return (double.NaN, double.NaN);
            }

            return (parsedValue, parsedValue + 0.5);
        }

        public static void CheckRatingBoundary(
            string bracketTitle,
            double? rating,
            (double lower, double upper) skillGroup,
            DuprPlayerInfo playerDupr,
            Dictionary<string, List<DuprPlayerInfo>> upperBoundary,
            Dictionary<string, List<DuprPlayerInfo>> lowerBoundary)
        {
            if (rating > skillGroup.upper)
                upperBoundary.GetOrAdd(bracketTitle).Add(playerDupr);

            if (rating < skillGroup.lower)
                lowerBoundary.GetOrAdd(bracketTitle).Add(playerDupr);
        }

        private static List<DuprPlayerInfo> GetOrAdd(
        this Dictionary<string, List<DuprPlayerInfo>> dict,
        string key)
        {
            if (!dict.ContainsKey(key))
                dict[key] = new List<DuprPlayerInfo>();

            return dict[key];
        }
    }
}
