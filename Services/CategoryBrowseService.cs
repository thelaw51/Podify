using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Podify.Services;

public record PodcastCategory(int Id, string Name, string Emoji);

public class CategoryBrowseService
{
    private readonly HttpClient _http;

    public CategoryBrowseService(HttpClient http)
    {
        _http = http;
    }

    public static IReadOnlyList<PodcastCategory> Categories { get; } = new[]
    {
        new PodcastCategory(1318, "Technology", "💻"),
        new PodcastCategory(1303, "Comedy", "😂"),
        new PodcastCategory(1489, "History", "🏛️"),
        new PodcastCategory(1488, "True Crime", "🔍"),
        new PodcastCategory(1321, "Business", "💼"),
        new PodcastCategory(1324, "News", "📰"),
        new PodcastCategory(1315, "Science", "🔬"),
        new PodcastCategory(1304, "Education", "🎓"),
        new PodcastCategory(1307, "Health & Fitness", "💪"),
        new PodcastCategory(1487, "Arts", "🎨"),
        new PodcastCategory(1310, "Music", "🎵"),
        new PodcastCategory(1316, "Sports", "🏀"),
        new PodcastCategory(1545, "Books", "📚"),
        new PodcastCategory(1323, "Society & Culture", "🌍"),
        new PodcastCategory(1311, "TV & Film", "🎬"),
        new PodcastCategory(1483, "Fiction", "📖"),
    };

    public async Task<List<PodcastSearchResult>> GetTopAsync(int genreId, int limit = 20, CancellationToken ct = default)
    {
        var rssUrl = $"https://itunes.apple.com/us/rss/toppodcasts/limit={limit}/genre={genreId}/json";
        var topFeed = await _http.GetFromJsonAsync<TopFeed>(rssUrl, ct);
        var ids = topFeed?.Feed?.Entry?
            .Select(e => e.Id?.Attributes?.ImId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? new();

        if (ids.Count == 0) return new();

        var lookupUrl = $"https://itunes.apple.com/lookup?id={string.Join(",", ids)}&entity=podcast";
        var lookup = await _http.GetFromJsonAsync<LookupResponse>(lookupUrl, ct);
        if (lookup?.Results is null) return new();

        var byId = lookup.Results.ToDictionary(r => r.CollectionId.ToString(), r => r);
        var ordered = new List<PodcastSearchResult>();
        foreach (var id in ids)
        {
            if (id is null) continue;
            if (!byId.TryGetValue(id, out var r)) continue;
            if (string.IsNullOrWhiteSpace(r.FeedUrl)) continue;
            ordered.Add(new PodcastSearchResult
            {
                CollectionId = r.CollectionId.ToString(),
                CollectionName = r.CollectionName ?? string.Empty,
                ArtistName = r.ArtistName ?? string.Empty,
                FeedUrl = r.FeedUrl ?? string.Empty,
                ArtworkUrl = r.ArtworkUrl600 ?? r.ArtworkUrl100 ?? string.Empty
            });
        }
        return ordered;
    }

    private class TopFeed
    {
        [JsonPropertyName("feed")] public TopFeedBody? Feed { get; set; }
    }
    private class TopFeedBody
    {
        [JsonPropertyName("entry")] public List<TopEntry>? Entry { get; set; }
    }
    private class TopEntry
    {
        [JsonPropertyName("id")] public TopId? Id { get; set; }
    }
    private class TopId
    {
        [JsonPropertyName("attributes")] public TopIdAttrs? Attributes { get; set; }
    }
    private class TopIdAttrs
    {
        [JsonPropertyName("im:id")] public string? ImId { get; set; }
    }
    private class LookupResponse
    {
        [JsonPropertyName("results")] public List<LookupResult>? Results { get; set; }
    }
    private class LookupResult
    {
        [JsonPropertyName("collectionId")] public long CollectionId { get; set; }
        [JsonPropertyName("collectionName")] public string? CollectionName { get; set; }
        [JsonPropertyName("artistName")] public string? ArtistName { get; set; }
        [JsonPropertyName("feedUrl")] public string? FeedUrl { get; set; }
        [JsonPropertyName("artworkUrl600")] public string? ArtworkUrl600 { get; set; }
        [JsonPropertyName("artworkUrl100")] public string? ArtworkUrl100 { get; set; }
    }
}
