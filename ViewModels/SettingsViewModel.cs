using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Services;

namespace Podify.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly PodcastDatabase _db;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _skipForwardLabel = string.Empty;
    [ObservableProperty] private string _skipBackLabel = string.Empty;
    [ObservableProperty] private string _defaultSpeedLabel = string.Empty;
    [ObservableProperty] private string _totalListeningLabel = "—";
    [ObservableProperty] private string _episodesCompletedLabel = "—";

    public SettingsViewModel(SettingsService settings, PodcastDatabase db)
    {
        _settings = settings;
        _db = db;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        SkipForwardLabel = FormatSeconds((int)_settings.SkipForwardDuration.TotalSeconds);
        SkipBackLabel = FormatSeconds((int)_settings.SkipBackDuration.TotalSeconds);
        DefaultSpeedLabel = $"{_settings.DefaultSpeed:0.##}×";
    }

    private static string FormatSeconds(int s) => s >= 60 ? $"{s / 60} min" : $"{s} sec";

    [RelayCommand]
    public async Task LoadStatsAsync()
    {
        IsLoading = true;
        try
        {
            var played = await _db.GetPlayedEpisodesAsync();
            var totalSeconds = played.Sum(e => e.Duration.TotalSeconds);
            var hours = (int)(totalSeconds / 3600);
            var mins = (int)(totalSeconds % 3600 / 60);
            TotalListeningLabel = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
            EpisodesCompletedLabel = played.Count.ToString();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PickSkipForwardAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Skip forward duration", "Cancel", null,
            "10 seconds", "15 seconds", "30 seconds", "45 seconds", "60 seconds");
        var seconds = choice switch
        {
            "10 seconds" => 10, "15 seconds" => 15, "30 seconds" => 30,
            "45 seconds" => 45, "60 seconds" => 60, _ => -1
        };
        if (seconds > 0) { _settings.SkipForwardDuration = TimeSpan.FromSeconds(seconds); RefreshLabels(); }
    }

    [RelayCommand]
    public async Task PickSkipBackAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Skip back duration", "Cancel", null,
            "5 seconds", "10 seconds", "15 seconds", "30 seconds");
        var seconds = choice switch
        {
            "5 seconds" => 5, "10 seconds" => 10, "15 seconds" => 15, "30 seconds" => 30, _ => -1
        };
        if (seconds > 0) { _settings.SkipBackDuration = TimeSpan.FromSeconds(seconds); RefreshLabels(); }
    }

    [RelayCommand]
    public async Task PickDefaultSpeedAsync()
    {
        var choice = await Shell.Current.DisplayActionSheetAsync(
            "Default playback speed", "Cancel", null,
            "0.75×", "1×", "1.25×", "1.5×", "1.75×", "2×");
        var speed = choice switch
        {
            "0.75×" => 0.75, "1×" => 1.0, "1.25×" => 1.25,
            "1.5×" => 1.5, "1.75×" => 1.75, "2×" => 2.0, _ => -1.0
        };
        if (speed > 0) { _settings.DefaultSpeed = speed; RefreshLabels(); }
    }
}
