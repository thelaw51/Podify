# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

**Prerequisites:** .NET 10 SDK + `dotnet workload install maui`. iOS / Mac Catalyst require Xcode.

```bash
# Build per platform
dotnet build -f net10.0-maccatalyst            # macOS
dotnet build -f net10.0-ios                    # iOS
dotnet build -f net10.0-android                # Android
dotnet build -f net10.0-windows10.0.19041.0    # Windows (host must be Windows — gated in csproj)

# Run (example: macOS)
dotnet build -t:Run -f net10.0-maccatalyst
```

No test project exists yet. See `Podify.csproj` for the full package list (MAUI 10.0.60, CommunityToolkit.Maui[.MediaElement], CommunityToolkit.Mvvm, sqlite-net-pcl).

## Architecture

**Stack:** .NET MAUI + C# 12 + XAML — one codebase targeting iOS, Android, macOS (Catalyst), and Windows.

**Pattern:** MVVM via `CommunityToolkit.Mvvm` source generators.
- `[ObservableProperty]` on fields auto-generates change-notifying properties.
- `[RelayCommand]` on methods auto-generates `IAsyncRelayCommand` wrapper commands.
- Pages are thin (code-behind only for navigation); all logic lives in ViewModels.
- Services are registered as singletons in `MauiProgram.cs` and constructor-injected into ViewModels.

**Key service responsibilities:**

| Service | Responsibility |
|---|---|
| `PodcastDatabase` | SQLite async CRUD for `Podcast` and `Episode` tables |
| `RssFeedService` | Parses RSS + iTunes-namespace XML into models |
| `PodcastSearchService` | iTunes Search API (no key required) |
| `CategoryBrowseService` | iTunes top-podcasts-by-genre feed, 16 hardcoded categories |
| `PlayerService` | `MediaElement` wrapper; fires `StateChanged` events; persists position every 5 s |
| `DownloadService` | HTTP streaming downloads tracked via `ConcurrentDictionary` |
| `OpmlImportService` | OPML parsing + bulk subscribe with live progress |

**Navigation:** 3-tab `AppShell` (Subscriptions / Discover / Player). Detail route: `podcast?id={id}` via `Shell.Current.GoToAsync()`.

**Identity:** Podcast ID = SHA-1(feedUrl)[:16] — re-subscribing to the same feed is idempotent. Episode ID = hash(podcastId + guid-or-audioUrl).

**Playback:** `PlayerService` debounces scrub input by 200 ms to avoid rapid seeks. Position is also saved on pause, episode switch, and `App.OnSleep()`. On open, playback resumes from any saved position > 2 s.

**Platform-specific:** `MauiProgram.cs` installs a custom `UITapGestureRecognizer` via `SliderHandler.Mapper` on iOS/macOS so tapping the slider seeks to that position (native UISlider does not do this by default).

**Theming:** `Resources/Colors.xaml` defines semantic tokens (`Primary` = violet #5B21B6, `Surface`, `Text.*`) using `AppThemeBinding` for automatic light/dark switching. `Resources/Styles.xaml` defines reusable type ramp (Display, H1, H2, Body, Caption, Eyebrow) and button variants (Primary, Ghost, Subtle, Icon).

## Project Layout

```
Models/         Plain data classes (Podcast, Episode + DownloadStatus enum)
Services/       All I/O and business logic — no UI dependencies
ViewModels/     ObservableObject subclasses; hold all UI state
Pages/          XAML + thin code-behind; bind to ViewModels via x:DataType
Resources/      Colors.xaml, Styles.xaml, fonts, images
Platforms/      Platform-specific entry points and manifests
MauiProgram.cs  DI container, handler overrides, font/plugin registration
AppShell.xaml   Tab bar and route definitions
```
