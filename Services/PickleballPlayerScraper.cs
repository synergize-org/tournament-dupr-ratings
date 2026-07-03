using HtmlAgilityPack;
using System.Collections.Concurrent;

namespace TournamentDuprRatings.Services
{
    internal class PickleballPlayerScraper
    {
        private static string _duprIdXpath = "//div[div[normalize-space(text())='DUPR ID']]/span";
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://pickleball.com/players/";
        // Static and concurrent: shared across instances/runs, and player lookups now run in parallel.
        private static ConcurrentDictionary<string, PlayerProfile> _cache = new ConcurrentDictionary<string, PlayerProfile>();

        public PickleballPlayerScraper(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public PickleballPlayerScraper()
        {
            _httpClient = new HttpClient();
        }

        public async Task<PlayerProfile> GetPlayerProfileAsync(string playerSlug)
        {
            if (_cache.TryGetValue(playerSlug, out var cachedProfile))
            {
                return cachedProfile;
            }

            var url = $"{BaseUrl}{Uri.EscapeDataString(playerSlug)}";

            var html = await FetchHtmlAsync(url);
            return _cache[playerSlug] = ParsePlayerProfile(html);
        }

        private async Task<string> FetchHtmlAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private PlayerProfile ParsePlayerProfile(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return new PlayerProfile
            {
                DuprId = GetNodeValue(doc, _duprIdXpath) ?? string.Empty,
            };
        }

        private static string? GetNodeValue(HtmlDocument doc, string xpath)
        {
            var node = doc.DocumentNode.SelectSingleNode(xpath);
            return node?.InnerText.Trim();
        }

        public record PlayerProfile
        {
            public required string DuprId { get; init; }
        }
    }
}
