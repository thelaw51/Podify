using SQLite;

namespace PodcastApp.Models;

public class Episode
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string PodcastId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public DateTime Published { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan PlayPosition { get; set; }
    public DateTime LastPlayedAt { get; set; }
    public bool IsPlayed { get; set; }
    public string? LocalFilePath { get; set; }
    public DownloadStatus DownloadStatus { get; set; }
    public int QueuePosition { get; set; } = -1;
}

public enum DownloadStatus
{
    NotDownloaded,
    Queued,
    Downloading,
    Downloaded,
    Failed
}
