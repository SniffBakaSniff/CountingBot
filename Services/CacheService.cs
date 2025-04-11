using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Serilog;

namespace CountingBot.Services
{
    /// <summary>
    /// Service for caching frequently accessed data to reduce database load.
    /// Implements a thread-safe in-memory cache with expiration.
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = [];
        private readonly TimeSpan _defaultExpiration;
        private readonly Timer _cleanupTimer;
        private readonly Dictionary<string, TimeSpan> _dataTypeExpirations;
        private const int CleanupIntervalMinutes = 10;

        // Cache key prefixes for different data types
        public const string UserInfoPrefix = "UserInfo_";
        public const string GuildSettingsPrefix = "GuildSettings_";
        public const string ChannelSettingsPrefix = "ChannelSettings_";
        public const string LanguagePrefix = "Language_";
        public const string CountingPrefix = "Counting_";

        /// <summary>
        /// Initializes a new instance of the CacheService with a default expiration time.
        /// </summary>
        /// <param name="defaultExpirationMinutes">Default cache expiration time in minutes.</param>
        public CacheService(int defaultExpirationMinutes = 30)
        {
            _defaultExpiration = TimeSpan.FromMinutes(defaultExpirationMinutes);
            _cleanupTimer = new Timer(
                CleanupCache,
                null,
                TimeSpan.FromMinutes(CleanupIntervalMinutes),
                TimeSpan.FromMinutes(CleanupIntervalMinutes)
            );

            // Configure different expiration times for different data types
            _dataTypeExpirations = new Dictionary<string, TimeSpan>
            {
                // User data changes less frequently
                { UserInfoPrefix, TimeSpan.FromMinutes(30) },
                // Guild settings change infrequently
                { GuildSettingsPrefix, TimeSpan.FromMinutes(60) },
                // Channel settings change infrequently
                { ChannelSettingsPrefix, TimeSpan.FromMinutes(60) },
                // Language strings are static
                { LanguagePrefix, TimeSpan.FromHours(24) },
                // Counting data changes frequently
                { CountingPrefix, TimeSpan.FromMinutes(5) },
            };
        }

        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Output value if found.</param>
        /// <returns>True if the value was found in the cache, false otherwise.</returns>
        public bool TryGetValue<T>(string key, out T? value)
        {
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                value = (T)entry.Value;
                Log.Debug("Cache hit for key: {Key}", key);

                return true;
            }

            value = default;
            Log.Debug("Cache miss for key: {Key}", key);

            return false;
        }

        /// <summary>
        /// Sets a value in the cache with the appropriate expiration time based on the key prefix.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to cache.</param>
        public void Set<T>(string key, T value)
        {
            // Determine the appropriate expiration time based on the key prefix
            TimeSpan expiration = _defaultExpiration;

            var prefix = _dataTypeExpirations.Keys.FirstOrDefault(p => key.StartsWith(p));
            if (prefix != null)
            {
                expiration = _dataTypeExpirations[prefix];
            }

            Set(key, value, expiration);
        }

        /// <summary>
        /// Sets a value in the cache with a custom expiration time.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to cache.</param>
        /// <param name="expiration">Custom expiration timespan.</param>
        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            var entry = new CacheEntry
            {
                Value = value!,
                ExpirationTime = DateTime.UtcNow.Add(expiration),
            };

            _cache[key] = entry;
            Log.Debug(
                "Added/updated cache entry for key: {Key}, expires in {ExpirationMinutes} minutes",
                key,
                expiration.TotalMinutes
            );
        }

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">Cache key to remove.</param>
        /// <returns>True if the value was removed, false if it wasn't in the cache.</returns>
        public bool Remove(string key)
        {
            var result = _cache.TryRemove(key, out _);
            if (result)
            {
                Log.Debug("Removed cache entry for key: {Key}", key);
            }
            return result;
        }

        /// <summary>
        /// Removes all values with keys that match the specified pattern.
        /// </summary>
        /// <param name="keyPattern">The pattern to match against cache keys.</param>
        public void RemoveByPattern(string keyPattern)
        {
            var keysToRemove = _cache.Keys.Where(k => k.Contains(keyPattern)).ToList();
            foreach (var key in keysToRemove)
            {
                Remove(key);
            }
            Log.Debug(
                "Removed {Count} cache entries matching pattern: {Pattern}",
                keysToRemove.Count,
                keyPattern
            );
        }

        /// <summary>
        /// Gets a value from the cache or computes it if not present.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="valueFactory">Function to compute the value if not in cache.</param>
        /// <returns>The cached or computed value.</returns>
        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory)
        {
            if (TryGetValue<T>(key, out var cachedValue) && cachedValue is not null)
            {
                return cachedValue;
            }

            var value = await valueFactory();
            Set(key, value);
            return value;
        }

        /// <summary>
        /// Cleans up expired cache entries.
        /// </summary>
        private void CleanupCache(object? state)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            Log.Debug(
                "Cache cleanup completed. Removed {Count} expired entries.",
                expiredKeys.Count
            );
        }

        /// <summary>
        /// Disposes resources used by the cache service.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by the cache service.
        /// </summary>
        /// <param name="disposing">Whether this is being called from Dispose() or the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cleanupTimer?.Dispose();
            }
        }

        /// <summary>
        /// Represents a cached entry with expiration time.
        /// </summary>
        private sealed class CacheEntry
        {
            public required object Value { get; init; }
            public DateTime ExpirationTime { get; init; }
            public bool IsExpired => DateTime.UtcNow > ExpirationTime;
        }
    }

    /// <summary>
    /// Interface for a service that provides caching functionality.
    /// </summary>
    public interface ICacheService : IDisposable
    {
        /// <summary>
        /// Gets a value from the cache.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Output value if found.</param>
        /// <returns>True if the value was found in the cache, false otherwise.</returns>
        bool TryGetValue<T>(string key, out T? value);

        /// <summary>
        /// Sets a value in the cache with the default expiration time.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to cache.</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// Sets a value in the cache with a custom expiration time.
        /// </summary>
        /// <typeparam name="T">Type of the value to cache.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to cache.</param>
        /// <param name="expiration">Custom expiration timespan.</param>
        void Set<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">Cache key to remove.</param>
        /// <returns>True if the value was removed, false if it wasn't in the cache.</returns>
        bool Remove(string key);

        /// <summary>
        /// Removes all values with keys that match the specified pattern.
        /// </summary>
        /// <param name="keyPattern">The pattern to match against cache keys.</param>
        void RemoveByPattern(string keyPattern);

        /// <summary>
        /// Gets a value from the cache or computes it if not present.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="valueFactory">Function to compute the value if not in cache.</param>
        /// <returns>The cached or computed value.</returns>
        Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory);
    }
}
