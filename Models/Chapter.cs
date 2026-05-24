namespace Podify.Models;

public class Chapter
{
    public TimeSpan Start { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Url { get; set; }
}
