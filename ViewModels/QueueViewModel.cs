using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

public class QueueItem
{
    public Episode Episode { get; }
    public string ArtworkUrl { get; }
    public string PodcastTitle { get; }
    public string DurationLabel { get; }

    public QueueItem(Episode episode, Podcast? podcast)
    {
        Episode = episode;
        ArtworkUrl = !string.IsNullOrWhiteSpace(episode.ArtworkUrl)
            ? episode.ArtworkUrl
            : podcast?.ArtworkUrl ?? string.Empty;
        PodcastTitle = podcast?.Title ?? string.Empty;
        var d = episode.Duration;
        DurationLabel = d.TotalHours >= 1 ? $"{(int)d.TotalHours}h {d.Minutes}m" : $"{(int)d.TotalMinutes}m";
    }
}

public partial class QueueViewModel : ObservableObject
{
    private readonly PodcastDatabase _db;
    private readonly PlayerService _player;

    [ObservableProperty]
    private ObservableCollection<QueueItem> _queue = new();

    [ObservableProperty]
    private bool _isLoading;

    public bool HasQueue => Queue.Count > 0;

    partial void OnQueueChanged(ObservableCollection<QueueItem> value) =>
        OnPropertyChanged(nameof(HasQueue));

    public QueueViewModel(PodcastDatabase db, PlayerService player)
    {
        _db = db;
        _player = player;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var episodes = await _db.GetQueueAsync();
            var podcastIds = episodes.Select(e => e.PodcastId).Distinct().ToList();
            var podcasts = new Dictionary<string, Podcast?>();
            foreach (var pid in podcastIds)
                podcasts[pid] = await _db.GetPodcastAsync(pid);

            Queue = new ObservableCollection<QueueItem>(
                episodes.Select(e => new QueueItem(e, podcasts.GetValueOrDefault(e.PodcastId))));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlayFromHereAsync(QueueItem item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync("//player");
        await _player.PlayAsync(item.Episode);
    }

    [RelayCommand]
    public async Task RemoveFromQueueAsync(QueueItem item)
    {
        if (item is null) return;
        item.Episode.QueuePosition = -1;
        await _db.UpdateEpisodeAsync(item.Episode);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ClearQueueAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync("Clear queue", "Remove all episodes from queue?", "Clear", "Cancel");
        if (!confirm) return;
        await _db.ClearQueueAsync();
        await LoadAsync();
    }
}
