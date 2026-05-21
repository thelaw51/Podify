# PodcastApp

A cross-platform podcast player built with .NET MAUI. Runs natively on iOS, Android, macOS (Catalyst), and Windows from a single C# codebase. Inspired by Pocket Casts.

## Features

- **Subscriptions** — square-art grid of your library with overflow menu (open / unsubscribe), pull-to-refresh
- **Continue listening** — carousel of in-progress episodes with progress bar and time-left, resumes from saved position
- **New today** — episodes published in the last 24 hours from subscribed feeds
- **Recently added** — most recent subscriptions
- **Discover**
  - Multiple top-shows carousels by category (Technology, True Crime, Comedy, News, History, Business, Science, Arts, …)
  - "Browse all categories" chip grid for the full list (taps add that category to the featured set)
  - Search via iTunes Search API
  - Subscribe button reflects current subscription state
- **Podcast detail** — hero header with artwork, episode list with Play / Queue / Download per episode
- **Player**
  - Big artwork hero with episode metadata
  - Scrub bar with drag-to-seek and tap-to-jump (custom iOS handler)
  - 15s back / 30s forward, variable speed (0.5×–2×)
  - Persists play position every 5 seconds while playing, and on pause / app sleep / episode switch
  - Auto-advances from the queue when an episode ends
- **OPML import** — bulk-subscribe from an OPML file picked via the system file picker, with live progress
- **Offline downloads** — episode caching with status tracking

## Stack

| Layer       | What                                                                 |
|-------------|----------------------------------------------------------------------|
| UI          | .NET MAUI (XAML pages) with CommunityToolkit.Maui controls           |
| MVVM        | CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| Audio       | CommunityToolkit.Maui.MediaElement (AVPlayer / ExoPlayer / MediaPlayer under the hood) |
| Persistence | sqlite-net-pcl, SQLite stored in `FileSystem.AppDataDirectory`       |
| Catalog     | iTunes Search + Top Podcasts RSS endpoints (no API key required)     |

## Project structure

```
PodcastApp/
├─ MauiProgram.cs            Entry point — DI registrations, handler tweaks (incl. iOS slider tap-to-seek)
├─ App.xaml / .xaml.cs       Application object; OnSleep saves player position
├─ AppShell.xaml / .xaml.cs  Tab bar definition + nested route registrations
├─ Models/                   Plain data (Podcast, Episode)
├─ Services/                 Logic & I/O
│   ├─ PodcastDatabase.cs       sqlite-net wrapper, queries
│   ├─ RssFeedService.cs        RSS + iTunes namespace parser
│   ├─ PodcastSearchService.cs  iTunes Search API
│   ├─ CategoryBrowseService.cs Top-podcasts-by-genre + bulk lookup
│   ├─ PlayerService.cs         MediaElement wrapper, position persistence
│   ├─ DownloadService.cs       Streaming HTTP downloads to AppDataDirectory
│   └─ OpmlImportService.cs     OPML parsing + batch subscribe
├─ ViewModels/               Bindable state + commands per page
├─ Pages/                    XAML views + thin code-behind
├─ Resources/
│   ├─ Colors.xaml              Semantic color tokens (light + dark)
│   ├─ Styles.xaml              Typography (Display/H1/H2/Body/Caption/Eyebrow) and components
│   ├─ Fonts/                   OpenSans
│   ├─ Images/                  Tab icons + assets
│   ├─ appicon.svg              App icon background (violet gradient)
│   └─ appiconfg.svg            App icon foreground (headphones mark)
└─ Platforms/                Per-platform entry points and manifests
```

## Building

Prerequisites:

- .NET 10 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- For iOS / Mac Catalyst: macOS + Xcode
- For Android: Android SDK + an emulator or device

Build per platform:

```bash
dotnet build -f net10.0-maccatalyst    # macOS desktop
dotnet build -f net10.0-ios            # iOS
dotnet build -f net10.0-android        # Android
dotnet build -f net10.0-windows10.0.19041.0   # Windows (on Windows hosts)
```

Run on a device or simulator:

```bash
dotnet build -t:Run -f net10.0-maccatalyst
```

For iOS device deployment you'll need an Apple Developer signing identity. See Apple's docs on signing or use Xcode to bootstrap a provisioning profile for the bundle ID (`com.joegillard.podcastapp`).

## Design notes

- **Identity by feed-URL hash.** A podcast's `Id` is `Hash(feedUrl)` (truncated SHA-1). Re-subscribing to the same feed is therefore idempotent (`InsertOrReplaceAsync`), no lookup-then-insert required.
- **MVVM triangle.** Pages bind to ViewModels; ViewModels call Services; Services know nothing of the UI.
- **TwoWay scrubbing.** The player slider binds TwoWay with a 200 ms debounce on `SeekToAsync`, plus a custom `UITapGestureRecognizer` for iOS/Mac Catalyst tap-to-jump (which `UISlider` doesn't natively support).
- **Resource-driven theming.** All colors and typography live in `Resources/*.xaml`; dark mode is automatic via `AppThemeBinding`.
