using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Helpers;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

[QueryProperty(nameof(EpisodeId), "id")]
public partial class EpisodeDetailViewModel : ObservableObject
{
    private readonly PodcastDatabase _db;
    private readonly PlayerService _player;
    private readonly DownloadService _downloads;

    [ObservableProperty]
    private string _episodeId = string.Empty;

    [ObservableProperty]
    private Episode? _episode;

    [ObservableProperty]
    private Podcast? _podcast;

    [ObservableProperty]
    private string _artworkUrl = string.Empty;

    [ObservableProperty]
    private bool _isPreview;

    [ObservableProperty]
    private bool _isLoading;

    public bool IsPlayed => Episode?.IsPlayed ?? false;
    public bool IsQueued => (Episode?.QueuePosition ?? -1) >= 0;
    public string PlayedLabel => IsPlayed ? "Mark as unplayed" : "Mark as played";
    public string QueueLabel => IsQueued ? "Remove from queue" : "Add to queue";
    public string DescriptionText => HtmlText.ToPlainText(Episode?.Description);
    public ObservableCollection<Chapter> Chapters { get; } = new();
    public bool HasChapters => Chapters.Count > 0;

    public EpisodeDetailViewModel(PodcastDatabase db, PlayerService player, DownloadService downloads)
    {
        _db = db;
        _player = player;
        _downloads = downloads;
    }

    partial void OnEpisodeIdChanged(string value) => _ = LoadAsync();

    partial void OnEpisodeChanged(Episode? value)
    {
        Chapters.Clear();
        foreach (var ch in LoadChapters(value)) Chapters.Add(ch);
        OnPropertyChanged(nameof(IsPlayed));
        OnPropertyChanged(nameof(IsQueued));
        OnPropertyChanged(nameof(PlayedLabel));
        OnPropertyChanged(nameof(QueueLabel));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(HasChapters));
    }

    private static List<Chapter> LoadChapters(Episode? episode)
    {
        if (episode is null || string.IsNullOrWhiteSpace(episode.ChaptersJson)) return new();
        try { return JsonSerializer.Deserialize<List<Chapter>>(episode.ChaptersJson) ?? new(); }
        catch { return new(); }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(EpisodeId)) return;
        IsLoading = true;
        try
        {
            var dbEpisode = await _db.GetEpisodeAsync(EpisodeId);
            if (dbEpisode is not null)
            {
                Episode = dbEpisode;
                Podcast = await _db.GetPodcastAsync(Episode.PodcastId);
                IsPreview = false;
            }
            else if (PreviewEpisodeCache.TryGet(EpisodeId, out var previewEp, out var previewPod))
            {
                Episode = previewEp;
                Podcast = previewPod;
                IsPreview = true;
            }
            else
            {
                return;
            }
            ArtworkUrl = !string.IsNullOrWhiteSpace(Episode!.ArtworkUrl)
                ? Episode.ArtworkUrl
                : Podcast?.ArtworkUrl ?? string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlayAsync()
    {
        if (Episode is null) return;
        await Shell.Current.GoToAsync("//player");
        await _player.PlayAsync(Episode);
    }

    [RelayCommand]
    public async Task ToggleQueueAsync()
    {
        if (Episode is null) return;
        if (Episode.QueuePosition >= 0)
        {
            Episode.QueuePosition = -1;
        }
        else
        {
            var queue = await _db.GetQueueAsync();
            Episode.QueuePosition = queue.Count;
        }
        await _db.UpdateEpisodeAsync(Episode);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task DownloadAsync()
    {
        if (Episode is null) return;
        await _downloads.DownloadAsync(Episode);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ToggleListenedAsync()
    {
        if (Episode is null) return;
        await _player.MarkPlayedAsync(Episode, !Episode.IsPlayed);
        await LoadAsync();
    }

    [RelayCommand]
    public async Task JumpToChapterAsync(Chapter chapter)
    {
        if (Episode is null || chapter is null) return;
        await Shell.Current.GoToAsync("//player");
        if (_player.CurrentEpisode?.Id == Episode.Id)
        {
            await _player.SeekToChapterAsync(chapter);
        }
        else
        {
            Episode.PlayPosition = chapter.Start;
            await _player.PlayAsync(Episode);
        }
    }
}
