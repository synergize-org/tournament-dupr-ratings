using CsvHelper.Configuration.Attributes;

namespace TournamentDuprRatings.Models
{
    public class CsvPlayerList
    {
        [Index(0)] public required string UserID { get; set; }
        [Index(1)] public required string FirstName { get; set; }
        [Index(2)] public required string LastName { get; set; }
        [Index(3)] public string? NameSuffix { get; set; }
        [Index(4)] public required string DUPR_ID { get; set; }
        [Index(5)] public string IsFreeAgent { get; set; }
        [Index(6)] public string Withdrawn { get; set; }
        [Index(7)] public required string PlayerLevel { get; set; }
        [Index(8)] public required string Gender { get; set; }
        [Index(9)] public int Age { get; set; }
        [Index(10)] public string? TeamName { get; set; }
        [Index(11)] public required string EventTitle { get; set; }
        [Index(12)] public string IsOnWaitingList { get; set; }
        [Index(13)] public string? Address1 { get; set; }
        [Index(14)] public string? Address2 { get; set; }
        [Index(15)] public string? Country_Abbr { get; set; }
        [Index(16)] public string? State_Abbr { get; set; }
        [Index(17)] public string? City { get; set; }
        [Index(18)] public string? Zip { get; set; }
        [Index(19)] public string? Phone { get; set; }
        [Index(20)] public string SendTexts { get; set; }
        [Index(21)] public required string Email { get; set; }
        [Index(22)] public string? EmergencyContact_Name { get; set; }
        [Index(23)] public string? EmergencyContact_Phone { get; set; }
        [Index(24)] public required string AttendeeHeaderID { get; set; }
        [Index(25)] public required string EventID { get; set; }
        [Index(26)] public required string AttendeeEventID { get; set; }
        [Index(27)] public string NeedPartner { get; set; }
        [Index(28)] public string? DateMovedToWaiting { get; set; }
        [Index(29)] public required string DateRegisteredForEvent { get; set; }
        [Index(30)] public required string Rating_AtSignup { get; set; }
        [Index(31)] public string? RatingUsed_AtSignup { get; set; }
        [Index(32)] public string? USAP_Num { get; set; }
        [Index(33)] public string? PCO_Num { get; set; }
        [Index(34)] public string? USSP_Num { get; set; }
        [Index(35)] public string? PAA_Num { get; set; }
        [Index(36)] public string? LifeTime_Num { get; set; }
    }
}
