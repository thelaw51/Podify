using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using Podify.Models;

namespace Podify.Services;

public class PlayerService
{
    private readonly PodcastDatabase _db;
    private MediaElement? _media;
    private Episode? _current;
    private Episode? _pending;
    private double _speed = 1.0;
    private TimeSpan _resumePosition = TimeSpan.Zero;
    private DateTime _lastPositionSaveAt = DateTime.MinValue;
    private static readonly TimeSpan SavePositionInterval = TimeSpan.FromSeconds(5);

    public event EventHandler? StateChanged;

    public PlayerService(PodcastDatabase db)
    {
        _db = db;
    }

    public Episode? CurrentEpisode => _current;
    public bool IsPlaying => _media?.CurrentState == MediaElementState.Playing;
    public TimeSpan Position => _media?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _media?.Duration ?? _current?.Duration ?? TimeSpan.Zero;
    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.5, 3.0);
            if (_media is not null) _media.Speed = _speed;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AttachMediaElement(MediaElement media)
    {
        if (ReferenceEquals(_media, media)) return;

        if (_media is not null)
        {
            _media.StateChanged -= OnMediaStateChanged;
            _media.PositionChanged -= OnMediaPositionChanged;
            _media.MediaEnded -= OnMediaEnded;
            _media.MediaOpened -= OnMediaOpened;
        }

        _media = media;
        _media.StateChanged += OnMediaStateChanged;
        _media.PositionChanged += OnMediaPositionChanged;
        _media.MediaEnded += OnMediaEnded;
        _media.MediaOpened += OnMediaOpened;
        _media.ShouldShowPlaybackControls = false;
        _media.ShouldAutoPlay = true;
        _media.Speed = _speed;

        if (_pending is not null)
        {
            var ep = _pending;
            _pending = null;
            _ = PlayAsync(ep);
        }
    }

    public Task PlayAsync(Episode episode)
    {
        if (_media is null)
        {
            _pending = episode;
            _current = episode;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        if (_current is not null && _current.Id != episode.Id)
        {
            _ = SaveCurrentPositionAsync();
        }

        _current = episode;
        _resumePosition = episode.PlayPosition > TimeSpan.FromSeconds(2) ? episode.PlayPosition : TimeSpan.Zero;

        var source = !string.IsNullOrWhiteSpace(episode.LocalFilePath) && File.Exists(episode.LocalFilePath)
            ? MediaSource.FromFile(episode.LocalFilePath)
            : MediaSource.FromUri(episode.AudioUrl);

        _media.ShouldAutoPlay = true;
        _media.Speed = _speed;
        _media.Source = source;

        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_media is null) return;
        if (_resumePosition > TimeSpan.Zero)
        {
            var target = _resumePosition;
            _resumePosition = TimeSpan.Zero;
            await _media.SeekTo(target);
        }
    }

    public void Pause() => _media?.Pause();
    public void Resume() => _media?.Play();
    public void TogglePlayPause()
    {
        if (_media is null) return;
        if (_media.CurrentState == MediaElementState.Playing) _media.Pause();
        else _media.Play();
    }

    public async Task SkipForwardAsync(TimeSpan span)
    {
        if (_media is null) return;
        var target = _media.Position + span;
        if (target > _media.Duration) target = _media.Duration;
        await _media.SeekTo(target);
    }

    public async Task SkipBackAsync(TimeSpan span)
    {
        if (_media is null) return;
        var target = _media.Position - span;
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        await _media.SeekTo(target);
    }

    public async Task SeekToAsync(TimeSpan position)
    {
        if (_media is null) return;
        await _media.SeekTo(position);
    }

    private async void OnMediaStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (e.NewState == MediaElementState.Paused)
        {
            await SaveCurrentPositionAsync();
        }
    }

    private async void OnMediaPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);

        if (_media?.CurrentState != MediaElementState.Playing) return;
        var now = DateTime.UtcNow;
        if (now - _lastPositionSaveAt < SavePositionInterval) return;
        _lastPositionSaveAt = now;
        await SaveCurrentPositionAsync();
    }

    public async Task SaveCurrentPositionAsync()
    {
        if (_current is null || _media is null) return;
        var position = _media.Position;
        if (position <= TimeSpan.Zero) return;
        _current.PlayPosition = position;
        _current.LastPlayedAt = DateTime.UtcNow;
        try { await _db.UpdateEpisodeAsync(_current); } catch { }
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        if (_current is null) return;
        _current.IsPlayed = true;
        _current.PlayPosition = TimeSpan.Zero;
        _current.QueuePosition = -1;
        await _db.UpdateEpisodeAsync(_current);
        await PlayNextFromQueueAsync();
    }

    private async Task PlayNextFromQueueAsync()
    {
        var queue = await _db.GetQueueAsync();
        var next = queue.FirstOrDefault();
        if (next is not null) await PlayAsync(next);
    }
}
