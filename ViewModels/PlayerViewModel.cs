using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly PlayerService _player;
    private readonly PodcastDatabase _db;
    private string? _resolvedForEpisodeId;

    [ObservableProperty]
    private Episode? _current;

    [ObservableProperty]
    private string _artworkUrl = string.Empty;

    [ObservableProperty]
    private string _podcastTitle = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _position;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _speed = 1.0;

    private bool _applyingFromPlayer;
    private CancellationTokenSource? _scrubCts;

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";
    public string PlayPauseGlyph => IsPlaying ? "" : ""; // pause / play_arrow
    public string SpeedLabel => $"{Speed:0.##}×";
    public bool HasArtwork => !string.IsNullOrWhiteSpace(ArtworkUrl);
    public string CurrentChapterTitle => _player.CurrentChapter?.Title ?? string.Empty;
    public bool HasChapter => _player.CurrentChapter is not null;

    partial void OnArtworkUrlChanged(string value) => OnPropertyChanged(nameof(HasArtwork));
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseGlyph));
    partial void OnSpeedChanged(double value) => OnPropertyChanged(nameof(SpeedLabel));

    public PlayerService Player => _player;

    public PlayerViewModel(PlayerService player, PodcastDatabase db)
    {
        _player = player;
        _db = db;
        _player.StateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Current = _player.CurrentEpisode;
            IsPlaying = _player.IsPlaying;
            OnPropertyChanged(nameof(PlayPauseLabel));
            Position = _player.Position;
            Duration = _player.Duration;
            if (_scrubCts is null)
            {
                _applyingFromPlayer = true;
                PositionSeconds = _player.Position.TotalSeconds;
                _applyingFromPlayer = false;
            }
            Speed = _player.Speed;
            OnPropertyChanged(nameof(CurrentChapterTitle));
            OnPropertyChanged(nameof(HasChapter));
            _ = ResolveArtworkAsync();
        });
    }

    partial void OnPositionSecondsChanged(double value)
    {
        if (_applyingFromPlayer) return;

        _scrubCts?.Cancel();
        var cts = new CancellationTokenSource();
        _scrubCts = cts;
        _ = DebouncedSeekAsync(TimeSpan.FromSeconds(value), cts);
    }

    private async Task DebouncedSeekAsync(TimeSpan target, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(200, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_scrubCts == cts) _scrubCts = null;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => _player.SeekToAsync(target));
        }
        catch
        {
        }
    }

    private async Task ResolveArtworkAsync()
    {
        var ep = Current;
        if (ep is null)
        {
            ArtworkUrl = string.Empty;
            _resolvedForEpisodeId = null;
            return;
        }
        if (_resolvedForEpisodeId == ep.Id) return;
        _resolvedForEpisodeId = ep.Id;

        var pod = await _db.GetPodcastAsync(ep.PodcastId);
        PodcastTitle = pod?.Title ?? string.Empty;
        ArtworkUrl = !string.IsNullOrWhiteSpace(ep.ArtworkUrl) ? ep.ArtworkUrl : pod?.ArtworkUrl ?? string.Empty;
    }

    [RelayCommand]
    public void TogglePlayPause() => _player.TogglePlayPause();

    [RelayCommand]
    public Task SkipForward() => _player.SkipForwardAsync(TimeSpan.FromSeconds(30));

    [RelayCommand]
    public Task SkipBack() => _player.SkipBackAsync(TimeSpan.FromSeconds(15));

    [RelayCommand]
    public void CycleSpeed()
    {
        var next = Speed switch
        {
            < 1.0 => 1.0,
            < 1.25 => 1.25,
            < 1.5 => 1.5,
            < 1.75 => 1.75,
            < 2.0 => 2.0,
            _ => 1.0
        };
        _player.Speed = next;
    }

    public void Dispose()
    {
        _player.StateChanged -= OnPlayerStateChanged;
    }
}
