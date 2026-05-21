using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using PodcastApp.Models;

namespace PodcastApp.Services;

public class RssFeedService
{
    private readonly HttpClient _http;

    private static readonly XNamespace ItunesNs = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    public RssFeedService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(Podcast Podcast, List<Episode> Episodes)> FetchAsync(string feedUrl, CancellationToken ct = default)
    {
        await using var stream = await _http.GetStreamAsync(feedUrl, ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);

        var channel = doc.Root?.Element("channel")
            ?? throw new InvalidDataException("Feed missing <channel> element");

        var title = channel.Element("title")?.Value?.Trim() ?? "Untitled";
        var description = channel.Element("description")?.Value?.Trim() ?? string.Empty;
        var author = channel.Element(ItunesNs + "author")?.Value?.Trim()
            ?? channel.Element("managingEditor")?.Value?.Trim()
            ?? string.Empty;
        var artworkUrl = channel.Element(ItunesNs + "image")?.Attribute("href")?.Value
            ?? channel.Element("image")?.Element("url")?.Value
            ?? string.Empty;

        var podcastId = Hash(feedUrl);
        var podcast = new Podcast
        {
            Id = podcastId,
            Title = title,
            Author = author,
            Description = description,
            FeedUrl = feedUrl,
            ArtworkUrl = artworkUrl,
            LastRefreshed = DateTime.UtcNow,
            SubscribedAt = DateTime.UtcNow
        };

        var episodes = new List<Episode>();
        foreach (var item in channel.Elements("item"))
        {
            var enclosure = item.Element("enclosure");
            var audioUrl = enclosure?.Attribute("url")?.Value;
            if (string.IsNullOrWhiteSpace(audioUrl)) continue;

            var guid = item.Element("guid")?.Value?.Trim();
            var episodeId = string.IsNullOrWhiteSpace(guid)
                ? Hash(podcastId + audioUrl)
                : Hash(podcastId + guid);

            var epTitle = item.Element("title")?.Value?.Trim() ?? "Untitled";
            var epDescription = item.Element(ItunesNs + "summary")?.Value?.Trim()
                ?? item.Element("description")?.Value?.Trim()
                ?? string.Empty;
            var pubDate = ParseDate(item.Element("pubDate")?.Value);
            var duration = ParseDuration(item.Element(ItunesNs + "duration")?.Value);
            var epArtwork = item.Element(ItunesNs + "image")?.Attribute("href")?.Value ?? string.Empty;

            episodes.Add(new Episode
            {
                Id = episodeId,
                PodcastId = podcastId,
                Title = epTitle,
                Description = epDescription,
                AudioUrl = audioUrl,
                ArtworkUrl = epArtwork,
                Published = pubDate,
                Duration = duration,
                PlayPosition = TimeSpan.Zero,
                IsPlayed = false,
                DownloadStatus = DownloadStatus.NotDownloaded,
                QueuePosition = -1
            });
        }

        return (podcast, episodes);
    }

    private static DateTime ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTime.UtcNow;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            return dt;
        return DateTime.UtcNow;
    }

    private static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;
        value = value.Trim();
        if (int.TryParse(value, out var seconds)) return TimeSpan.FromSeconds(seconds);

        var parts = value.Split(':');
        try
        {
            return parts.Length switch
            {
                3 => new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])),
                2 => new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1])),
                _ => TimeSpan.Zero
            };
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static string Hash(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
