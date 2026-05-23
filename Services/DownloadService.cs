using System.Collections.Concurrent;
using Podify.Models;

namespace Podify.Services;

public class DownloadService
{
    private readonly HttpClient _http;
    private readonly PodcastDatabase _db;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new();

    public event EventHandler<Episode>? DownloadProgressChanged;
    public event EventHandler<Episode>? DownloadCompleted;

    public DownloadService(HttpClient http, PodcastDatabase db)
    {
        _http = http;
        _db = db;
    }

    private static string DownloadsRoot
    {
        get
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "Downloads");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public async Task DownloadAsync(Episode episode)
    {
        if (_active.ContainsKey(episode.Id)) return;
        if (episode.DownloadStatus == DownloadStatus.Downloaded
            && !string.IsNullOrEmpty(episode.LocalFilePath)
            && File.Exists(episode.LocalFilePath))
            return;

        var cts = new CancellationTokenSource();
        _active[episode.Id] = cts;

        try
        {
            episode.DownloadStatus = DownloadStatus.Downloading;
            await _db.UpdateEpisodeAsync(episode);
            DownloadProgressChanged?.Invoke(this, episode);

            var extension = Path.GetExtension(new Uri(episode.AudioUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5) extension = ".mp3";
            var targetPath = Path.Combine(DownloadsRoot, $"{episode.Id}{extension}");

            using var response = await _http.GetAsync(episode.AudioUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(cts.Token))
            await using (var dest = File.Create(targetPath))
            {
                await source.CopyToAsync(dest, 81920, cts.Token);
            }

            episode.LocalFilePath = targetPath;
            episode.DownloadStatus = DownloadStatus.Downloaded;
            await _db.UpdateEpisodeAsync(episode);
            DownloadCompleted?.Invoke(this, episode);
        }
        catch (OperationCanceledException)
        {
            episode.DownloadStatus = DownloadStatus.NotDownloaded;
            await _db.UpdateEpisodeAsync(episode);
        }
        catch
        {
            episode.DownloadStatus = DownloadStatus.Failed;
            await _db.UpdateEpisodeAsync(episode);
        }
        finally
        {
            _active.TryRemove(episode.Id, out _);
        }
    }

    public void Cancel(string episodeId)
    {
        if (_active.TryGetValue(episodeId, out var cts)) cts.Cancel();
    }

    public async Task DeleteDownloadAsync(Episode episode)
    {
        if (!string.IsNullOrEmpty(episode.LocalFilePath) && File.Exists(episode.LocalFilePath))
        {
            try { File.Delete(episode.LocalFilePath); } catch { }
        }
        episode.LocalFilePath = null;
        episode.DownloadStatus = DownloadStatus.NotDownloaded;
        await _db.UpdateEpisodeAsync(episode);
    }
}
