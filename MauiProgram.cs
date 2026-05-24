using CommunityToolkit.Maui;
using Podify.Pages;
using Podify.Services;
using Podify.ViewModels;
#if IOS || MACCATALYST
using UIKit;
using Microsoft.Maui.Handlers;
#endif

namespace Podify;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: true)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

#if IOS || MACCATALYST
        SliderHandler.Mapper.AppendToMapping("TapToSeek", (handler, view) =>
        {
            var uiSlider = handler.PlatformView;

            if (uiSlider.GestureRecognizers is not null)
            {
                foreach (var existing in uiSlider.GestureRecognizers
                    .OfType<UITapGestureRecognizer>()
                    .Where(g => g.Name == "MauiTapToSeek")
                    .ToList())
                {
                    uiSlider.RemoveGestureRecognizer(existing);
                }
            }

            var tap = new UITapGestureRecognizer(g =>
            {
                if (g.View is not UISlider native) return;
                var location = g.LocationInView(native);
                var width = native.Bounds.Width;
                if (width <= 0) return;
                var fraction = Math.Clamp(location.X / width, 0d, 1d);
                var newValue = view.Minimum + fraction * (view.Maximum - view.Minimum);
                view.Value = newValue;
            })
            {
                Name = "MauiTapToSeek"
            };

            uiSlider.AddGestureRecognizer(tap);
        });
#endif

        builder.Services.AddSingleton<HttpClient>(_ =>
        {
            var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Podify/0.1");
            return http;
        });

        builder.Services.AddSingleton<PodcastDatabase>();
        builder.Services.AddSingleton<RssFeedService>();
        builder.Services.AddSingleton<PodcastSearchService>();
        builder.Services.AddSingleton<CategoryBrowseService>();
        builder.Services.AddSingleton<OpmlImportService>();
        builder.Services.AddSingleton<DownloadService>();
        builder.Services.AddSingleton<PlayerService>();

        builder.Services.AddSingleton<SubscriptionsViewModel>();
        builder.Services.AddSingleton<DiscoverViewModel>();
        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddTransient<PodcastDetailViewModel>();
        builder.Services.AddTransient<EpisodeDetailViewModel>();

        builder.Services.AddSingleton<SubscriptionsPage>();
        builder.Services.AddSingleton<DiscoverPage>();
        builder.Services.AddSingleton<PlayerPage>();
        builder.Services.AddTransient<PodcastDetailPage>();
        builder.Services.AddTransient<EpisodeDetailPage>();

        return builder.Build();
    }
}
