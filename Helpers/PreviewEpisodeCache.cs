using Podify.Models;

namespace Podify.Helpers;

/// <summary>
/// In-memory handoff for preview (un-subscribed) podcast data between the
/// podcast-detail and episode-detail pages. Preview episodes have no SQLite
/// row, so navigation can't be resolved by id alone.
/// </summary>
public static class PreviewEpisodeCache
{
    private static readonly Dictionary<string, (Episode Episode, Podcast Podcast)> _entries = new();
    private static readonly object _gate = new();

    public static void Set(Podcast podcast, IEnumerable<Episode> episodes)
    {
        if (podcast is null || episodes is null) return;
        lock (_gate)
        {
            _entries.Clear();
            foreach (var ep in episodes)
            {
                if (!string.IsNullOrEmpty(ep.Id)) _entries[ep.Id] = (ep, podcast);
            }
        }
    }

    public static bool TryGet(string episodeId, out Episode? episode, out Podcast? podcast)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(episodeId, out var entry))
            {
                episode = entry.Episode;
                podcast = entry.Podcast;
                return true;
            }
        }
        episode = null;
        podcast = null;
        return false;
    }
}
