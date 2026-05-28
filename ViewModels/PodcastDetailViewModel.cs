using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Helpers;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

[QueryProperty(nameof(PodcastId), "id")]
[QueryProperty(nameof(PreviewFeedUrl), "feed")]
public partial class PodcastDetailViewModel : ObservableObject
{
    private readonly PodcastDatabase _db;
    private readonly PlayerService _player;
    private readonly DownloadService _downloads;
    private readonly RssFeedService _rss;

    [ObservableProperty]
    private string _podcastId = string.Empty;

    [ObservableProperty]
    private string _previewFeedUrl = string.Empty;

    [ObservableProperty]
    private Podcast? _podcast;

    [ObservableProperty]
    private ObservableCollection<Episode> _episodes = new();

    [ObservableProperty]
    private bool _isPreview;

    [ObservableProperty]
    private bool _isLoading;

    public PodcastDetailViewModel(PodcastDatabase db, PlayerService player, DownloadService downloads, RssFeedService rss)
    {
        _db = db;
        _player = player;
        _downloads = downloads;
        _rss = rss;
    }

    partial void OnPodcastIdChanged(string value) => _ = LoadAsync();

    partial void OnPreviewFeedUrlChanged(string value) => _ = LoadPreviewAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(PodcastId)) return;
        IsLoading = true;
        try
        {
            Podcast = await _db.GetPodcastAsync(PodcastId);
            var eps = await _db.GetEpisodesAsync(PodcastId);
            Episodes = new ObservableCollection<Episode>(eps);
            IsPreview = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(PreviewFeedUrl)) return;
        IsLoading = true;
        try
        {
            var (podcast, episodes) = await _rss.FetchAsync(PreviewFeedUrl);
            Podcast = podcast;
            Episodes = new ObservableCollection<Episode>(episodes);
            IsPreview = true;
            PreviewEpisodeCache.Set(podcast, episodes);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't load preview", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SubscribeFromPreviewAsync()
    {
        if (!IsPreview || Podcast is null) return;
        try
        {
            await _db.UpsertPodcastAsync(Podcast);
            await _db.UpsertEpisodesAsync(Episodes);
            PodcastId = Podcast.Id;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Subscribe failed", ex.Message, "OK");
        }
    }

    [RelayCommand]
    public async Task PlayAsync(Episode episode)
    {
        if (episode is null) return;
        await Shell.Current.GoToAsync("//player");
        await _player.PlayAsync(episode);
    }

    [RelayCommand]
    public async Task ShareAsync()
    {
        if (Podcast is null) return;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = Podcast.Title,
            Text = $"Check out {Podcast.Title}",
            Uri = Podcast.FeedUrl
        });
    }

    [RelayCommand]
    public Task OpenEpisodeAsync(Episode episode)
    {
        if (episode is null) return Task.CompletedTask;
        return Shell.Current.GoToAsync($"episode?id={episode.Id}");
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
