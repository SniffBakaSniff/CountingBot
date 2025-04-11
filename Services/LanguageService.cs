using System.Collections.Concurrent;
using System.Text.Json;

namespace CountingBot.Services
{
    /// <summary>
    /// Service responsible for managing and providing localized strings for the bot.
    /// Implements a thread-safe caching mechanism to improve performance of language file loading.
    /// </summary>
    public class LanguageService : ILanguageService
    {
        /// <summary>
        /// Thread-safe cache of loaded language dictionaries.
        /// Key is the language code (e.g., "en"), value is the dictionary of localized strings.
        /// </summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _languageCache =
            new ConcurrentDictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// External cache service for caching individual string lookups.
        /// </summary>
        private readonly ICacheService? _cacheService;
        private const string CacheKeyPrefix = "Lang_";

        /// <summary>
        /// Initializes a new instance of the LanguageService.
        /// </summary>
        public LanguageService()
        {
            _cacheService = null;
        }

        /// <summary>
        /// Initializes a new instance of the LanguageService with external caching support.
        /// </summary>
        /// <param name="cacheService">The cache service to use.</param>
        public LanguageService(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Retrieves a localized string for the specified key and language.
        /// Falls back to English if the requested language is not available.
        /// </summary>
        /// <param name="key">The key of the localized string to retrieve</param>
        /// <param name="language">The language code (e.g., "en" for English)</param>
        /// <returns>The localized string if found; otherwise returns the key itself</returns>
        public async Task<string> GetLocalizedStringAsync(string key, string language)
        {
            // Try to get from external cache first if available
            string cacheKey = $"{CacheKeyPrefix}{language}_{key}";
            if (
                _cacheService != null
                && _cacheService.TryGetValue<string>(cacheKey, out var cachedValue)
                && cachedValue != null
            )
            {
                return cachedValue;
            }

            // Not in external cache, load from language dictionary
            var strings = await LoadLanguageAsync(language);
            var result = strings.TryGetValue(key, out var value) ? value : key;

            // Store in external cache for future lookups
            _cacheService?.Set(cacheKey, result);

            return result;
        }

        /// <summary>
        /// Loads a language file from disk or retrieves it from cache.
        /// Implements a fallback mechanism to English if the requested language file is not found.
        /// </summary>
        /// <param name="language">The language code to load</param>
        /// <returns>Dictionary containing the language strings</returns>
        private async Task<Dictionary<string, string>> LoadLanguageAsync(string language)
        {
            if (_languageCache.TryGetValue(language, out var cachedDict))
                return cachedDict;

            string basePath = AppContext.BaseDirectory;
            string filePath = Path.Combine(basePath, "Data/Languages", $"{language}.json");

            if (!File.Exists(filePath))
                filePath = Path.Combine(basePath, "Data/Languages", "English.json");

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var dict =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();

                _languageCache.TryAdd(language, dict);
                return dict;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading language file for {language}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
    }

    /// <summary>
    /// Interface defining the contract for language services in the bot.
    /// </summary>
    public interface ILanguageService
    {
        /// <summary>
        /// Retrieves a localized string for the specified key and language.
        /// </summary>
        /// <param name="key">The key of the localized string to retrieve</param>
        /// <param name="language">The language code (e.g., "en" for English)</param>
        /// <returns>The localized string if found; otherwise returns the key itself</returns>
        Task<string> GetLocalizedStringAsync(string key, string language);
    }
}
