namespace TournamentDuprRatings.Models.Geocoding
{
    public class GeocodeResponse
    {
        public string Status { get; set; } = "";
        public List<GeocodeResult> Results { get; set; } = [];
    }
}
