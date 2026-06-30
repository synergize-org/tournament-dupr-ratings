using Newtonsoft.Json;

namespace TournamentDuprRatings.Models
{
    public class DuprPlayerInfo
    {
        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("result")]
        public DuprPlayerResult Result { get; set; } = new DuprPlayerResult();
    }

    public class DuprPlayerResult
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; } = "";

        [JsonProperty("shortAddress")]
        public string? ShortAddress { get; set; }

        [JsonProperty("gender")]
        public string? Gender { get; set; }

        [JsonProperty("age")]
        public int? Age { get; set; }

        [JsonProperty("ratings")]
        public DuprPlayerRatings Ratings { get; set; } = new DuprPlayerRatings();

        [JsonProperty("enablePrivacy")]
        public bool EnablePrivacy { get; set; }

        [JsonProperty("isPlayer1")]
        public bool IsPlayer1 { get; set; }

        [JsonProperty("verifiedEmail")]
        public bool VerifiedEmail { get; set; }

        [JsonProperty("registered")]
        public bool Registered { get; set; }

        [JsonProperty("duprId")]
        public string DuprId { get; set; } = "";

        [JsonProperty("showRatingBanner")]
        public bool ShowRatingBanner { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("sponsor")]
        public object? Sponsor { get; set; }

        [JsonProperty("lucraConnected")]
        public bool LucraConnected { get; set; }
    }

    public class DuprPlayerRatings
    {
        [JsonProperty("singles")]
        public string? Singles { get; set; }

        [JsonProperty("singlesVerified")]
        public string? SinglesVerified { get; set; }

        [JsonProperty("singlesProvisional")]
        public bool SinglesProvisional { get; set; }

        [JsonProperty("doubles")]
        public string? Doubles { get; set; }

        [JsonProperty("doublesVerified")]
        public string? DoublesVerified { get; set; }

        [JsonProperty("doublesProvisional")]
        public bool DoublesProvisional { get; set; }

        [JsonProperty("defaultRating")]
        public string? DefaultRating { get; set; }

        [JsonProperty("provisionalRatings")]
        public DuprProvisionalRatings ProvisionalRatings { get; set; } = new DuprProvisionalRatings();
    }

    public class DuprProvisionalRatings
    {
        [JsonProperty("singlesRating")]
        public double? SinglesRating { get; set; }

        [JsonProperty("doublesRating")]
        public double? DoublesRating { get; set; }

        [JsonProperty("coach")]
        public DuprCoach? Coach { get; set; }
    }

    public class DuprCoach
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("metadata")]
        public object? Metadata { get; set; }
    }
}
