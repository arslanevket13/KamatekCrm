using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace KamatekCrm.API.Services
{
    /// <summary>
    /// Cache-aside pattern implementasyonu.
    /// TTL Stratejisi:
    ///   Dashboard widgets → 30 saniye
    ///   Listeler (kategori, kullanıcı) → 5 dakika
    ///   Raporlar → 15 dakika
    /// </summary>
    public interface ICacheService
    {
        /// <summary>Cache'den oku, yoksa factory çalıştır ve cache'e yaz</summary>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

        /// <summary>Cache'den oku</summary>
        T? Get<T>(string key);

        /// <summary>Cache'e yaz</summary>
        void Set<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>Belirli anahtarı cache'den sil</summary>
        void Remove(string key);

        /// <summary>Prefix ile eşleşen tüm cache'leri sil (invalidation)</summary>
        void RemoveByPrefix(string prefix);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly HashSet<string> _cacheKeys = new();
        private readonly object _keysLock = new();

        // Öntanımlı TTL değerleri
        public static readonly TimeSpan DashboardTtl = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan ReportTtl = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        public CacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                Log.Debug("Cache HIT: {Key}", key);
                return cachedValue;
            }

            Log.Debug("Cache MISS: {Key} — fetching from source", key);
            var value = await factory();

            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiration ?? DefaultTtl)
                .SetSlidingExpiration(TimeSpan.FromMinutes(1))
                .RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
                {
                    Log.Debug("Cache evicted: {Key} reason={Reason}", evictedKey, reason);
                    lock (_keysLock)
                    {
                        _cacheKeys.Remove(evictedKey.ToString()!);
                    }
                });

            _cache.Set(key, value, options);
            TrackKey(key);

            return value;
        }

        public T? Get<T>(string key)
        {
            _cache.TryGetValue(key, out T? value);
            return value;
        }

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiration ?? DefaultTtl)
                .RegisterPostEvictionCallback((evictedKey, _, _, _) =>
                {
                    lock (_keysLock)
                    {
                        _cacheKeys.Remove(evictedKey.ToString()!);
                    }
                });

            _cache.Set(key, value, options);
            TrackKey(key);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            lock (_keysLock)
            {
                _cacheKeys.Remove(key);
            }
        }

        /// <summary>
        /// Prefix-based invalidation: örneğin "dashboard:" ile başlayan tüm cache'leri temizle
        /// </summary>
        public void RemoveByPrefix(string prefix)
        {
            List<string> keysToRemove;
            lock (_keysLock)
            {
                keysToRemove = _cacheKeys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            lock (_keysLock)
            {
                foreach (var key in keysToRemove)
                    _cacheKeys.Remove(key);
            }

            Log.Information("Cache invalidated: {Prefix}* ({Count} keys)", prefix, keysToRemove.Count);
        }

        private void TrackKey(string key)
        {
            lock (_keysLock)
            {
                _cacheKeys.Add(key);
            }
        }
    }

    /// <summary>
    /// Cache key sabitleri — magic string'leri önler
    /// </summary>
    public static class CacheKeys
    {
        public const string DashboardStats = "dashboard:stats";
        public const string DashboardWeeklyTrend = "dashboard:weekly-trend";
        public const string DashboardJobDistribution = "dashboard:job-distribution";
        public const string DashboardStatusDistribution = "dashboard:status-distribution";
        public const string Categories = "lists:categories";
        public const string Users = "lists:users";
        public const string TechnicianList = "lists:technicians";

        /// <summary>Dinamik key: "servicejob:{id}"</summary>
        public static string ServiceJob(int id) => $"servicejob:{id}";
        /// <summary>Dinamik key: "customer:{id}"</summary>
        public static string Customer(int id) => $"customer:{id}";
    }
}
