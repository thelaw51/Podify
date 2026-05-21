using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PodcastApp.Services;

public class PodcastSearchResult
{
    public string CollectionId { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
}

public class PodcastSearchService
{
    private readonly HttpClient _http;

    public PodcastSearchService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PodcastSearchResult>> SearchAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return new();

        var url = $"https://itunes.apple.com/search?media=podcast&limit=30&term={Uri.EscapeDataString(term)}";
        var response = await _http.GetFromJsonAsync<ITunesSearchResponse>(url, ct);
        if (response?.Results is null) return new();

        return response.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.FeedUrl))
            .Select(r => new PodcastSearchResult
            {
                CollectionId = r.CollectionId.ToString(),
                CollectionName = r.CollectionName ?? string.Empty,
                ArtistName = r.ArtistName ?? string.Empty,
                FeedUrl = r.FeedUrl ?? string.Empty,
                ArtworkUrl = r.ArtworkUrl600 ?? r.ArtworkUrl100 ?? string.Empty
            })
            .ToList();
    }

    private class ITunesSearchResponse
    {
        [JsonPropertyName("results")]
        public List<ITunesResult>? Results { get; set; }
    }

    private class ITunesResult
    {
        [JsonPropertyName("collectionId")]
        public long CollectionId { get; set; }

        [JsonPropertyName("collectionName")]
        public string? CollectionName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("feedUrl")]
        public string? FeedUrl { get; set; }

        [JsonPropertyName("artworkUrl600")]
        public string? ArtworkUrl600 { get; set; }

        [JsonPropertyName("artworkUrl100")]
        public string? ArtworkUrl100 { get; set; }
    }
}
