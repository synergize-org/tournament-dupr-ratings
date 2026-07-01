using Newtonsoft.Json;

namespace TournamentDuprRatings.Models.PbTournamentsModels
{
    public class EventStatus
    {
        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

    }
}
