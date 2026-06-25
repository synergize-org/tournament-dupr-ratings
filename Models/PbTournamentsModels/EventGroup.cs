using Newtonsoft.Json;

namespace TournamentDuprRatings.Models.PbTournamentsModels
{
    public class EventGroup
    {
        [JsonProperty("groupTitle")]
        public string GroupTitle { get; set; }

        [JsonProperty("events")]
        public List<TournamentEvent> Events { get; set; }

    }
}
