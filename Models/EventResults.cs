using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentDuprRatings.Models
{
    public class EventResults
    {
        public List<TeamResult> TeamResults { get; set; } = new List<TeamResult>();
        public string SkillGroup { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
