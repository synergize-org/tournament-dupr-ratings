using Newtonsoft.Json;

namespace TournamentDuprRatings.Models.PbTournamentsModels
{
    public class TournamentEventsResponse
    {
        [JsonProperty("events")]
        public List<EventGroup>? Events { get; set; }

        [JsonProperty("tourneyId")]
        public string? TourneyId { get; set; }

        [JsonProperty("tourneyPlayerCountHidden")]
        public bool TourneyPlayerCountHidden { get; set; }

        [JsonProperty("tourneyPlayersHidden")]
        public bool TourneyPlayersHidden { get; set; }

        [JsonProperty("tourneyFindPartnerHidden")]
        public bool TourneyFindPartnerHidden { get; set; }

    }
}
