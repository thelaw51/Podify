using Podify.Models;
using SQLite;

namespace Podify.Services;

public class PodcastDatabase
{
   private SQLiteAsyncConnection? _db;

   private async Task<SQLiteAsyncConnection> ConnectAsync()
   {
      if (_db is not null) return _db;
      var dbPath = Path.Combine(FileSystem.AppDataDirectory, "podcasts.db3");
      _db = new SQLiteAsyncConnection(dbPath,
         SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
      await _db.CreateTableAsync<Podcast>();
      await _db.CreateTableAsync<Episode>();
      return _db;
   }

   public async Task<List<Podcast>> GetSubscriptionsAsync()
   {
      var db = await ConnectAsync();
      return await db.Table<Podcast>().OrderBy(p => p.Title).ToListAsync();
   }

   public async Task<Podcast?> GetPodcastAsync(string id)
   {
      var db = await ConnectAsync();
      return await db.FindAsync<Podcast>(id);
   }

   public async Task UpsertPodcastAsync(Podcast podcast)
   {
      var db = await ConnectAsync();
      await db.InsertOrReplaceAsync(podcast);
   }

   public async Task DeletePodcastAsync(string id)
   {
      var db = await ConnectAsync();
      await db.DeleteAsync<Podcast>(id);
      await db.ExecuteAsync("DELETE FROM Episode WHERE PodcastId = ?", id);
   }

   public async Task<List<Episode>> GetEpisodesAsync(string podcastId)
   {
      var db = await ConnectAsync();
      return await db.Table<Episode>().Where(e => e.PodcastId == podcastId).OrderByDescending(e => e.Published)
         .ToListAsync();
   }

   public async Task<Episode?> GetEpisodeAsync(string id)
   {
      var db = await ConnectAsync();
      return await db.FindAsync<Episode>(id);
   }

   public async Task UpsertEpisodesAsync(IEnumerable<Episode> episodes)
   {
      var db = await ConnectAsync();
      await db.RunInTransactionAsync(tx =>
      {
         foreach (var ep in episodes)
         {
            var existing = tx.Find<Episode>(ep.Id);
            if (existing is null)
            {
               tx.Insert(ep);
            }
            else
            {
               existing.Title = ep.Title;
               existing.Description = ep.Description;
               existing.AudioUrl = ep.AudioUrl;
               existing.ArtworkUrl = ep.ArtworkUrl;
               existing.Published = ep.Published;
               existing.Duration = ep.Duration;
               if (ep.ChaptersJson is not null) existing.ChaptersJson = ep.ChaptersJson;
               tx.Update(existing);
            }
         }
      });
   }

   public async Task UpdateEpisodeAsync(Episode episode)
   {
      var db = await ConnectAsync();
      await db.UpdateAsync(episode);
   }

   public async Task<List<Episode>> GetQueueAsync()
   {
      var db = await ConnectAsync();
      return await db.Table<Episode>().Where(e => e.QueuePosition >= 0).OrderBy(e => e.QueuePosition).ToListAsync();
   }

   public async Task<List<Episode>> GetInProgressEpisodesAsync(int limit = 12)
   {
      var db = await ConnectAsync();
      var threshold = TimeSpan.FromSeconds(60);
      return await db.Table<Episode>().Where(e => e.PlayPosition > threshold && !e.IsPlayed)
         .OrderByDescending(e => e.LastPlayedAt).Take(limit).ToListAsync();
   }

   public async Task<List<Episode>> GetNewEpisodesAsync()
   {
      var db = await ConnectAsync();
      var since = DateTime.UtcNow - TimeSpan.FromHours(24);
      return await db.Table<Episode>()
         .Where(e => e.Published >= since)
         .OrderByDescending(e => e.Published)
         .ToListAsync();
   }

   public async Task<List<Episode>> GetDownloadedAsync()
   {
      var db = await ConnectAsync();
      return await db.Table<Episode>().Where(e => e.DownloadStatus == DownloadStatus.Downloaded)
         .OrderByDescending(e => e.Published).ToListAsync();
   }

   public async Task ClearQueueAsync()
   {
      var db = await ConnectAsync();
      await db.ExecuteAsync("UPDATE Episode SET QueuePosition = -1 WHERE QueuePosition >= 0");
   }
}