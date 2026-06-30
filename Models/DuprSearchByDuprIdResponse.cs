using Newtonsoft.Json;

namespace TournamentDuprRatings.Models
{
    public class DuprSearchByDuprIdResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("results")]
        public List<DuprSearchResult> Results { get; set; } = new List<DuprSearchResult>();

        [JsonProperty("errors")]
        public List<object> Errors { get; set; } = new List<object>();
    }

    public class DuprSearchResult
    {
        [JsonProperty("userId")]
        public long UserId { get; set; }
    }
}
