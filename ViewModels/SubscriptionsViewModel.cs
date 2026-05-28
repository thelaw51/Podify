using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Podify.Models;
using Podify.Services;

namespace Podify.ViewModels;

public class InProgressItem
{
   public Episode Episode { get; }
   public string ArtworkUrl { get; }
   public string PodcastTitle { get; }
   public double ProgressFraction { get; }
   public string TimeLeftLabel { get; }

   public InProgressItem(Episode episode, Podcast podcast)
   {
      Episode = episode;
      ArtworkUrl = !string.IsNullOrWhiteSpace(episode.ArtworkUrl)
         ? episode.ArtworkUrl
         : podcast?.ArtworkUrl ?? string.Empty;
      PodcastTitle = podcast?.Title ?? string.Empty;
      ProgressFraction = episode.Duration.TotalSeconds > 0
         ? Math.Clamp(episode.PlayPosition.TotalSeconds / episode.Duration.TotalSeconds, 0, 1)
         : 0;
      var left = episode.Duration - episode.PlayPosition;
      if (left < TimeSpan.Zero) left = TimeSpan.Zero;
      TimeLeftLabel = left.TotalHours >= 1
         ? $"{(int)left.TotalHours}h {left.Minutes}m left"
         : $"{(int)left.TotalMinutes}m left";
   }
}

public class TodaysEpisodeItem
{
   public Episode Episode { get; }
   public string ArtworkUrl { get; }
   public string PodcastTitle { get; }
   public string DurationLabel { get; }

   public TodaysEpisodeItem(Episode episode, Podcast podcast)
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

public partial class SubscriptionsViewModel : ObservableObject
{
   private readonly PodcastDatabase _db;
   private readonly RssFeedService _rss;
   private readonly OpmlImportService _opml;
   private readonly PlayerService _player;
   [ObservableProperty] private ObservableCollection<Podcast> _subscriptions = new();
   [ObservableProperty] private ObservableCollection<Podcast> _recentlyAdded = new();
   [ObservableProperty] private ObservableCollection<InProgressItem> _inProgress = new();
   [ObservableProperty] private ObservableCollection<TodaysEpisodeItem> _todaysEpisodes = new();
   public bool HasTodaysEpisodes => TodaysEpisodes.Count > 0;
   public bool HasRecentlyAdded => RecentlyAdded.Count > 0;
   public bool HasInProgress => InProgress.Count > 0;
   [ObservableProperty] private bool _isRefreshing;
   [ObservableProperty] private bool _isImporting;
   [ObservableProperty] private string _importStatus = string.Empty;
   public bool HasSubscriptions => Subscriptions.Count > 0;

   private DateTime _lastAutoRefresh = DateTime.MinValue;
   private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromMinutes(5);

   public SubscriptionsViewModel(PodcastDatabase db, RssFeedService rss, OpmlImportService opml, PlayerService player)
   {
      _db = db;
      _rss = rss;
      _opml = opml;
      _player = player;
   }

   partial void OnSubscriptionsChanged(ObservableCollection<Podcast> value) =>
      OnPropertyChanged(nameof(HasSubscriptions));

   [RelayCommand]
   public async Task LoadAsync()
   {
      var subs = await _db.GetSubscriptionsAsync();
      var orderedSubs = subs.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase).ToList();
      var recent = subs.OrderByDescending(s => s.SubscribedAt).Take(Math.Min(10, subs.Count)).ToList();
      var inProgressEpisodes = await _db.GetInProgressEpisodesAsync();
      var byId = subs.ToDictionary(p => p.Id);
      var inProgressItems = inProgressEpisodes.Where(e => byId.ContainsKey(e.PodcastId))
         .Select(e => new InProgressItem(e, byId[e.PodcastId])).ToList();
      var newEpisodes = await _db.GetNewEpisodesAsync();
      var newTodaysEpisodes = newEpisodes
         .Where(e => byId.ContainsKey(e.PodcastId))
         .Select(e => new TodaysEpisodeItem(e, byId[e.PodcastId]))
         .ToList();
      Subscriptions = new ObservableCollection<Podcast>(orderedSubs);
      TodaysEpisodes = new ObservableCollection<TodaysEpisodeItem>(newTodaysEpisodes);
      RecentlyAdded = new ObservableCollection<Podcast>(recent);
      InProgress = new ObservableCollection<InProgressItem>(inProgressItems);
      OnPropertyChanged(nameof(HasRecentlyAdded));
      OnPropertyChanged(nameof(HasInProgress));
      OnPropertyChanged(nameof(HasTodaysEpisodes));
   }

   [RelayCommand]
   public async Task PlayNewEpisodeAsync(TodaysEpisodeItem item)
   {
      if (item is null) return;
      await Shell.Current.GoToAsync("//player");
      await _player.PlayAsync(item.Episode);
   }
   
   [RelayCommand]
   public async Task ResumeAsync(InProgressItem item)
   {
      if (item is null) return;
      await Shell.Current.GoToAsync("//player");
      await _player.PlayAsync(item.Episode);
   }

   public async Task AutoRefreshOnAppearAsync()
   {
      if (IsRefreshing) return;
      if (DateTime.UtcNow - _lastAutoRefresh < AutoRefreshInterval) return;
      await RefreshAllAsync();
   }

   [RelayCommand]
   public async Task RefreshAllAsync()
   {
      if (IsRefreshing) return;
      IsRefreshing = true;
      _lastAutoRefresh = DateTime.UtcNow;
      try
      {
         var failures = 0;
         foreach (var pod in Subscriptions.ToList())
         {
            try
            {
               var (refreshed, episodes) = await _rss.FetchAsync(pod.FeedUrl);
               refreshed.SubscribedAt = pod.SubscribedAt;
               await _db.UpsertPodcastAsync(refreshed);
               await _db.UpsertEpisodesAsync(episodes);
            }
            catch
            {
               failures++;
            }
         }

         await LoadAsync();
         if (failures > 0)
            await Shell.Current.DisplayAlertAsync("Refresh", $"{failures} podcast{(failures == 1 ? "" : "s")} couldn't be refreshed.", "OK");
      }
      finally
      {
         IsRefreshing = false;
      }
   }

   [RelayCommand]
   public async Task OpenPodcastAsync(Podcast podcast)
   {
      if (podcast is null) return;
      await Shell.Current.GoToAsync($"podcast?id={podcast.Id}");
   }

   [RelayCommand]
   public async Task UnsubscribeAsync(Podcast podcast)
   {
      if (podcast is null) return;
      var confirm =
         await Shell.Current.DisplayAlertAsync("Unsubscribe", $"Remove {podcast.Title}?", "Remove", "Cancel");
      if (!confirm) return;
      await _db.DeletePodcastAsync(podcast.Id);
      await LoadAsync();
      OnPropertyChanged(nameof(HasSubscriptions));
   }

   [RelayCommand]
   public async Task ShowPodcastMenuAsync(Podcast podcast)
   {
      if (podcast is null) return;
      var choice = await Shell.Current.DisplayActionSheetAsync(podcast.Title, "Cancel", null, "Open", "Unsubscribe");
      switch (choice)
      {
         case "Open":
            await OpenPodcastAsync(podcast);
            break;
         case "Unsubscribe":
            await UnsubscribeAsync(podcast);
            break;
      }
   }

   [RelayCommand]
   public Task OpenDownloadsAsync() => Shell.Current.GoToAsync("downloads");

   [RelayCommand]
   public async Task ImportOpmlAsync()
   {
      try
      {
         var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
         {
            { DevicePlatform.iOS, new[] { "public.xml", "org.opml.opml", "public.data" } },
            { DevicePlatform.MacCatalyst, new[] { "public.xml", "org.opml.opml", "public.data" } },
            { DevicePlatform.Android, new[] { "text/xml", "application/xml", "*/*" } },
            { DevicePlatform.WinUI, new[] { ".opml", ".xml" } }
         });
         var file = await FilePicker.Default.PickAsync(new PickOptions
         {
            PickerTitle = "Choose an OPML file", FileTypes = fileTypes
         });
         if (file is null) return;
         IsImporting = true;
         ImportStatus = "Reading file…";
         await using var stream = await file.OpenReadAsync();
         var progress = new Progress<OpmlImportProgress>(p =>
         {
            ImportStatus = p.CurrentTitle is null
               ? $"Imported {p.Imported}, skipped {p.Failed}"
               : $"({p.Processed + 1}/{p.Total}) {p.CurrentTitle}";
         });
         var result = await _opml.ImportAsync(stream, progress);
         await LoadAsync();
         await Shell.Current.DisplayAlertAsync("OPML Import",
            $"Added {result.Imported} of {result.Total} podcasts." +
            (result.Failed > 0 ? $"\n{result.Failed} failed to load." : ""), "OK");
      }
      catch (Exception ex)
      {
         await Shell.Current.DisplayAlertAsync("Import failed", ex.Message, "OK");
      }
      finally
      {
         IsImporting = false;
         ImportStatus = string.Empty;
      }
   }
}