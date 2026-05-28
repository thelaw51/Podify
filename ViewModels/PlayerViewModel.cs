using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly PlayerService _player;
    private readonly PodcastDatabase _db;
    private readonly SettingsService _settings;
    private string? _resolvedForEpisodeId;
    private string? _lastEpisodeId;

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
    public string SleepTimerLabel
    {
        get
        {
            if (!_player.SleepTimerActive) return "Sleep";
            var r = _player.SleepTimerRemaining;
            return r.TotalMinutes >= 1 ? $"Sleep {(int)r.TotalMinutes}m" : $"Sleep {r.Seconds}s";
        }
    }
    public bool SleepTimerActive => _player.SleepTimerActive;
    public int SkipForwardSeconds => (int)_settings.SkipForwardDuration.TotalSeconds;
    public int SkipBackSeconds => (int)_settings.SkipBackDuration.TotalSeconds;

    partial void OnArtworkUrlChanged(string value) => OnPropertyChanged(nameof(HasArtwork));
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseGlyph));
    partial void OnSpeedChanged(double value) => OnPropertyChanged(nameof(SpeedLabel));

    public PlayerService Player => _player;

    public PlayerViewModel(PlayerService player, PodcastDatabase db, SettingsService settings)
    {
        _player = player;
        _db = db;
        _settings = settings;
        _player.StateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var ep = _player.CurrentEpisode;

            // Apply default speed when a new episode starts
            if (ep?.Id != _lastEpisodeId)
            {
                _lastEpisodeId = ep?.Id;
                if (ep is not null && _settings.DefaultSpeed != _player.Speed)
                    _player.Speed = _settings.DefaultSpeed;
            }

            Current = ep;
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
            OnPropertyChanged(nameof(SleepTimerLabel));
            OnPropertyChanged(nameof(SleepTimerActive));
            OnPropertyChanged(nameof(SkipForwardSeconds));
            OnPropertyChanged(nameof(SkipBackSeconds));
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
    public Task SkipForward() => _player.SkipForwardAsync(_settings.SkipForwardDuration);

    [RelayCommand]
    public Task SkipBack() => _player.SkipBackAsync(_settings.SkipBackDuration);

    [RelayCommand]
    public async Task SetSleepTimerAsync()
    {
        var destructive = _player.SleepTimerActive ? "Cancel timer" : null;
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Sleep timer", "Dismiss", destructive,
            "15 minutes", "30 minutes", "45 minutes", "60 minutes");
        switch (choice)
        {
            case "15 minutes": _player.StartSleepTimer(TimeSpan.FromMinutes(15)); break;
            case "30 minutes": _player.StartSleepTimer(TimeSpan.FromMinutes(30)); break;
            case "45 minutes": _player.StartSleepTimer(TimeSpan.FromMinutes(45)); break;
            case "60 minutes": _player.StartSleepTimer(TimeSpan.FromMinutes(60)); break;
            case "Cancel timer": _player.CancelSleepTimer(); break;
        }
    }

    [RelayCommand]
    public Task OpenQueueAsync() => Shell.Current.GoToAsync("queue");

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
