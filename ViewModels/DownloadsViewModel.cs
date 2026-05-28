using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

public class DownloadedItem
{
    public Episode Episode { get; }
    public string ArtworkUrl { get; }
    public string PodcastTitle { get; }
    public long FileSizeBytes { get; }
    public string MetaLabel { get; }

    public DownloadedItem(Episode episode, Podcast? podcast)
    {
        Episode = episode;
        ArtworkUrl = !string.IsNullOrWhiteSpace(episode.ArtworkUrl)
            ? episode.ArtworkUrl
            : podcast?.ArtworkUrl ?? string.Empty;
        PodcastTitle = podcast?.Title ?? string.Empty;

        var d = episode.Duration;
        var durationLabel = d.TotalHours >= 1 ? $"{(int)d.TotalHours}h {d.Minutes}m" : $"{(int)d.TotalMinutes}m";

        if (!string.IsNullOrEmpty(episode.LocalFilePath) && File.Exists(episode.LocalFilePath))
        {
            FileSizeBytes = new FileInfo(episode.LocalFilePath).Length;
            var sizeLabel = FileSizeBytes >= 1_048_576
                ? $"{FileSizeBytes / 1_048_576.0:F0} MB"
                : $"{FileSizeBytes / 1024.0:F0} KB";
            MetaLabel = $"{durationLabel} · {sizeLabel}";
        }
        else
        {
            FileSizeBytes = 0;
            MetaLabel = durationLabel;
        }
    }
}

public partial class DownloadsViewModel : ObservableObject
{
    private readonly PodcastDatabase _db;
    private readonly PlayerService _player;
    private readonly DownloadService _downloadService;

    [ObservableProperty]
    private ObservableCollection<DownloadedItem> _downloads = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _storageSummary = string.Empty;

    public bool HasDownloads => Downloads.Count > 0;

    partial void OnDownloadsChanged(ObservableCollection<DownloadedItem> value) =>
        OnPropertyChanged(nameof(HasDownloads));

    public DownloadsViewModel(PodcastDatabase db, PlayerService player, DownloadService downloadService)
    {
        _db = db;
        _player = player;
        _downloadService = downloadService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var episodes = await _db.GetDownloadedAsync();
            var podcastIds = episodes.Select(e => e.PodcastId).Distinct().ToList();
            var podcasts = new Dictionary<string, Podcast?>();
            foreach (var pid in podcastIds)
                podcasts[pid] = await _db.GetPodcastAsync(pid);

            var items = episodes
                .Select(e => new DownloadedItem(e, podcasts.GetValueOrDefault(e.PodcastId)))
                .ToList();
            Downloads = new ObservableCollection<DownloadedItem>(items);

            var totalBytes = items.Sum(i => i.FileSizeBytes);
            StorageSummary = totalBytes >= 1_048_576
                ? $"{totalBytes / 1_048_576.0:F0} MB used"
                : $"{totalBytes / 1024.0:F0} KB used";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlayAsync(DownloadedItem item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync("//player");
        await _player.PlayAsync(item.Episode);
    }

    [RelayCommand]
    public async Task DeleteDownloadAsync(DownloadedItem item)
    {
        if (item is null) return;
        await _downloadService.DeleteDownloadAsync(item.Episode);
        await LoadAsync();
    }
}
