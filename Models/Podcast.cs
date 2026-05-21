using SQLite;

namespace PodcastApp.Models;

public class Podcast
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    
    public string Author { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public string FeedUrl { get; set; } = string.Empty;
    
    public string ArtworkUrl { get; set; } = string.Empty;
    
    public DateTime LastRefreshed { get; set; }
    
    public DateTime SubscribedAt { get; set; }
}
