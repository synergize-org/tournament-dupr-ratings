using Newtonsoft.Json;

namespace TournamentDuprRatings.Models.PbTournamentsModels
{
    public class TournamentEvent
    {
        [JsonProperty("playerGroup")]
        public string? PlayerGroup { get; set; }

        [JsonProperty("numOfRegistered")]
        public int NumOfRegistered { get; set; }

        [JsonProperty("numOfWaitlist")]
        public int NumOfWaitlist { get; set; }

        [JsonProperty("numOfLottery")]
        public int NumOfLottery { get; set; }

        [JsonProperty("format")]
        public string? Format { get; set; }

        [JsonProperty("formatId")]
        public int FormatId { get; set; }

        [JsonProperty("skillGroup")]
        public required string SkillGroup { get; set; }

        [JsonProperty("ageGroup")]
        public string? AgeGroup { get; set; }

        [JsonProperty("date")]
        public string? Date { get; set; }

        [JsonProperty("title")]
        public required string Title { get; set; }

        [JsonProperty("subtitle")]
        public string? Subtitle { get; set; }

        [JsonProperty("bracketType")]
        public string? BracketType { get; set; }

        [JsonProperty("bracketFormatId")]
        public int BracketFormatId { get; set; }

        [JsonProperty("maxNumOfTeams")]
        public int MaxNumOfTeams { get; set; }

        [JsonProperty("status")]
        public EventStatus? Status { get; set; }

        [JsonProperty("multipleDates")]
        public List<string>? MultipleDates { get; set; }

        [JsonProperty("eventId")]
        public string? EventId { get; set; }

        [JsonProperty("activitySplitId")]
        public string? ActivitySplitId { get; set; }

        [JsonProperty("activityId")]
        public string? ActivityId { get; set; }

        [JsonProperty("isCanceled")]
        public bool IsCanceled { get; set; }

        [JsonProperty("goldMedalTeam")]
        public string? GoldMedalTeam { get; set; }

        [JsonProperty("silverMedalTeam")]
        public string? SilverMedalTeam { get; set; }

        [JsonProperty("bronzeMedalTeam")]
        public string? BronzeMedalTeam { get; set; }

        [JsonProperty("players")]
        public List<object>? Players { get; set; }

        [JsonProperty("lotteryActive")]
        public bool LotteryActive { get; set; }

        [JsonProperty("waitlistActive")]
        public bool WaitlistActive { get; set; }

        [JsonProperty("showDraws")]
        public bool ShowDraws { get; set; }

        [JsonProperty("bracketLevelId")]
        public int BracketLevelId { get; set; }

        [JsonProperty("hidePlayers")]
        public bool HidePlayers { get; set; }

        [JsonProperty("currentSequence")]
        public int CurrentSequence { get; set; }

        [JsonProperty("currentSequencePoolId")]
        public string? CurrentSequencePoolId { get; set; }

        [JsonProperty("totalPools")]
        public int TotalPools { get; set; }

        /// <summary>
        /// Whether this event is currently full (registered >= max).
        /// </summary>
        [JsonIgnore]
        public bool IsFull => NumOfRegistered >= MaxNumOfTeams;

        /// <summary>
        /// Number of open spots remaining.
        /// </summary>
        [JsonIgnore]
        public int SpotsRemaining => MaxNumOfTeams - NumOfRegistered;

    }
}
