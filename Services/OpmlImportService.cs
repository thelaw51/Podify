using System.Xml.Linq;

namespace Podify.Services;

public record OpmlEntry(string Title, string FeedUrl);

public record OpmlImportProgress(int Processed, int Total, int Imported, int Failed, string? CurrentTitle);

public class OpmlImportService
{
    private readonly RssFeedService _rss;
    private readonly PodcastDatabase _db;

    public OpmlImportService(RssFeedService rss, PodcastDatabase db)
    {
        _rss = rss;
        _db = db;
    }

    public static List<OpmlEntry> Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var entries = new List<OpmlEntry>();
        foreach (var outline in doc.Descendants("outline"))
        {
            var feed = outline.Attribute("xmlUrl")?.Value
                ?? outline.Attribute("XMLURL")?.Value;
            if (string.IsNullOrWhiteSpace(feed)) continue;
            var title = outline.Attribute("text")?.Value
                ?? outline.Attribute("title")?.Value
                ?? feed;
            entries.Add(new OpmlEntry(title, feed));
        }
        return entries;
    }

    public async Task<OpmlImportProgress> ImportAsync(
        Stream stream,
        IProgress<OpmlImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var entries = Parse(stream);
        var total = entries.Count;
        var imported = 0;
        var failed = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries[i];
            progress?.Report(new OpmlImportProgress(i, total, imported, failed, entry.Title));
            try
            {
                var (podcast, episodes) = await _rss.FetchAsync(entry.FeedUrl, ct);
                await _db.UpsertPodcastAsync(podcast);
                await _db.UpsertEpisodesAsync(episodes);
                imported++;
            }
            catch
            {
                failed++;
            }
        }

        var done = new OpmlImportProgress(total, total, imported, failed, null);
        progress?.Report(done);
        return done;
    }
}
