using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

[QueryProperty(nameof(PodcastId), "id")]
public partial class PodcastDetailViewModel : ObservableObject
{
    private readonly PodcastDatabase _db;
    private readonly PlayerService _player;
    private readonly DownloadService _downloads;

    [ObservableProperty]
    private string _podcastId = string.Empty;

    [ObservableProperty]
    private Podcast? _podcast;

    [ObservableProperty]
    private ObservableCollection<Episode> _episodes = new();

    public PodcastDetailViewModel(PodcastDatabase db, PlayerService player, DownloadService downloads)
    {
        _db = db;
        _player = player;
        _downloads = downloads;
    }

    partial void OnPodcastIdChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(PodcastId)) return;
        Podcast = await _db.GetPodcastAsync(PodcastId);
        var eps = await _db.GetEpisodesAsync(PodcastId);
        Episodes = new ObservableCollection<Episode>(eps);
    }

    [RelayCommand]
    public async Task PlayAsync(Episode episode)
    {
        if (episode is null) return;
        await Shell.Current.GoToAsync("//player");
        await _player.PlayAsync(episode);
    }

    [RelayCommand]
    public async Task DownloadAsync(Episode episode)
    {
        if (episode is null) return;
        await _downloads.DownloadAsync(episode);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ToggleQueueAsync(Episode episode)
    {
        if (episode is null) return;
        if (episode.QueuePosition >= 0)
        {
            episode.QueuePosition = -1;
        }
        else
        {
            var queue = await _db.GetQueueAsync();
            episode.QueuePosition = queue.Count;
        }
        await _db.UpdateEpisodeAsync(episode);
        await LoadAsync();
    }
}
