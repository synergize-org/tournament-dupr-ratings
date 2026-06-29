namespace TournamentDuprRatings.Models;

public class EventPlayer
{
    public string? PlayerFullName { get; set; }
    public string? PartnerFullName { get; set; }
    public string? PlayerSlug { get; set; }
    public string? PartnerSlug { get; set; }
    public string? PlayerSkill { get; set; }
    public string? PartnerSkill { get; set; }
    public bool PlayerDuprActive { get; set; }
    public bool PartnerDuprActive { get; set; }
    public bool NeedAPartner { get; set; }
    public bool IsOnWaitlist { get; set; }
}
