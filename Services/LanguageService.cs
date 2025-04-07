using System.Collections.Concurrent;
using System.Text.Json;

namespace CountingBot.Services
{
    public class LanguageService : ILanguageService
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _languageCache 
            = new ConcurrentDictionary<string, Dictionary<string, string>>();

        public async Task<string> GetLocalizedStringAsync(string key, string language)
        {
            Dictionary<string, string> strings = await LoadLanguageAsync(language);
            return strings.TryGetValue(key, out var localized) ? localized : key;
        }

        private async Task<Dictionary<string, string>> LoadLanguageAsync(string language)
        {
            if (_languageCache.TryGetValue(language, out var cachedStrings))
                return cachedStrings;

            string basePath = AppContext.BaseDirectory;
            string filePath = Path.Combine(basePath, "Data/Languages", $"{language}.json");
            if (!File.Exists(filePath))
                filePath = Path.Combine(basePath, "Data/Languages", "en.json");

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
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

    public interface ILanguageService
    {
        Task<string> GetLocalizedStringAsync(string key, string language);
    }
}
