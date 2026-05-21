using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodcastApp.Services;

namespace PodcastApp.ViewModels;

public partial class DiscoverItem : ObservableObject
{
    public PodcastSearchResult Source { get; }

    public string CollectionName => Source.CollectionName;
    public string ArtistName => Source.ArtistName;
    public string ArtworkUrl => Source.ArtworkUrl;
    public string FeedUrl => Source.FeedUrl;

    [ObservableProperty]
    private bool _isSubscribed;

    public string SubscribeLabel => IsSubscribed ? "Subscribed" : "Subscribe";

    partial void OnIsSubscribedChanged(bool value) => OnPropertyChanged(nameof(SubscribeLabel));

    public DiscoverItem(PodcastSearchResult source, bool isSubscribed)
    {
        Source = source;
        _isSubscribed = isSubscribed;
    }
}

public partial class FeaturedRow : ObservableObject
{
    public PodcastCategory Category { get; }
    public string Header => $"{Category.Emoji}  Top {Category.Name}";

    [ObservableProperty]
    private ObservableCollection<DiscoverItem> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    public FeaturedRow(PodcastCategory category) => Category = category;
}

public partial class DiscoverViewModel : ObservableObject
{
    private readonly PodcastSearchService _search;
    private readonly RssFeedService _rss;
    private readonly PodcastDatabase _db;
    private readonly CategoryBrowseService _categories;
    private readonly HashSet<string> _subscribedFeedUrls = new(StringComparer.OrdinalIgnoreCase);

    private static readonly int[] FeaturedCategoryIds =
    {
        1318, 1488, 1303, 1324, 1489, 1321, 1315, 1487
    };

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasSearchResults;

    [ObservableProperty]
    private ObservableCollection<DiscoverItem> _results = new();

    public ObservableCollection<FeaturedRow> FeaturedRows { get; } = new();
    public ObservableCollection<PodcastCategory> AllCategories { get; } =
        new(CategoryBrowseService.Categories);

    public DiscoverViewModel(
        PodcastSearchService search,
        RssFeedService rss,
        PodcastDatabase db,
        CategoryBrowseService categories)
    {
        _search = search;
        _rss = rss;
        _db = db;
        _categories = categories;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshSubscribedSetAsync();

        if (FeaturedRows.Count == 0)
        {
            var byId = CategoryBrowseService.Categories.ToDictionary(c => c.Id);
            var rows = FeaturedCategoryIds
                .Where(byId.ContainsKey)
                .Select(id => new FeaturedRow(byId[id]))
                .ToList();
            foreach (var row in rows) FeaturedRows.Add(row);
            await Task.WhenAll(rows.Select(LoadRowAsync));
        }
        else
        {
            ReapplySubscribedFlags();
        }
    }

    private async Task RefreshSubscribedSetAsync()
    {
        var subs = await _db.GetSubscriptionsAsync();
        _subscribedFeedUrls.Clear();
        foreach (var s in subs)
        {
            if (!string.IsNullOrWhiteSpace(s.FeedUrl))
                _subscribedFeedUrls.Add(s.FeedUrl);
        }
    }

    private void ReapplySubscribedFlags()
    {
        foreach (var row in FeaturedRows)
            foreach (var item in row.Items)
                item.IsSubscribed = _subscribedFeedUrls.Contains(item.FeedUrl);

        foreach (var item in Results)
            item.IsSubscribed = _subscribedFeedUrls.Contains(item.FeedUrl);
    }

    [RelayCommand]
    public async Task ShowCategoryAsync(PodcastCategory category)
    {
        if (category is null) return;
        var existing = FeaturedRows.FirstOrDefault(r => r.Category.Id == category.Id);
        if (existing is not null)
        {
            FeaturedRows.Move(FeaturedRows.IndexOf(existing), 0);
            return;
        }
        var row = new FeaturedRow(category);
        FeaturedRows.Insert(0, row);
        await LoadRowAsync(row);
    }

    private async Task LoadRowAsync(FeaturedRow row)
    {
        row.IsLoading = true;
        try
        {
            var items = await _categories.GetTopAsync(row.Category.Id, limit: 15);
            row.Items = new ObservableCollection<DiscoverItem>(
                items.Select(r => new DiscoverItem(r, _subscribedFeedUrls.Contains(r.FeedUrl))));
        }
        catch
        {
            row.Items = new ObservableCollection<DiscoverItem>();
        }
        finally
        {
            row.IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            Results = new ObservableCollection<DiscoverItem>();
            HasSearchResults = false;
            return;
        }
        IsSearching = true;
        try
        {
            var hits = await _search.SearchAsync(Query);
            Results = new ObservableCollection<DiscoverItem>(
                hits.Select(r => new DiscoverItem(r, _subscribedFeedUrls.Contains(r.FeedUrl))));
            HasSearchResults = true;
        }
        catch
        {
            Results = new ObservableCollection<DiscoverItem>();
            HasSearchResults = true;
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        Query = string.Empty;
        Results = new ObservableCollection<DiscoverItem>();
        HasSearchResults = false;
    }

    [RelayCommand]
    public async Task SubscribeAsync(DiscoverItem item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.FeedUrl) || item.IsSubscribed) return;
        try
        {
            var (podcast, episodes) = await _rss.FetchAsync(item.FeedUrl);
            if (string.IsNullOrWhiteSpace(podcast.ArtworkUrl)) podcast.ArtworkUrl = item.ArtworkUrl;
            await _db.UpsertPodcastAsync(podcast);
            await _db.UpsertEpisodesAsync(episodes);

            _subscribedFeedUrls.Add(podcast.FeedUrl);
            ReapplySubscribedFlags();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Subscribe failed", ex.Message, "OK");
        }
    }
}
