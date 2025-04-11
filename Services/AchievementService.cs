using CountingBot.Database;
using CountingBot.Database.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace CountingBot.Services
{
    /// <summary>
    /// Service responsible for managing and tracking user achievements in the bot.
    /// Handles loading achievement definitions, checking requirements, and unlocking achievements.
    /// </summary>
    public class AchievementService
    {
        private readonly Dictionary<string, AchievementDefinition> _achievements = new();
        private readonly BotDbContext _dbContext;
        private bool _achievementsLoaded = false;

        /// <summary>
        /// Initializes a new instance of the AchievementService.
        /// </summary>
        /// <param name="dbContext">The database context for accessing user data</param>
        public AchievementService(BotDbContext dbContext)
        {
            LoadAchievements();
            _dbContext = dbContext;
        }

        /// <summary>
        /// Loads achievement definitions from the JSON configuration file.
        /// Only loads once and caches the results for subsequent access.
        /// </summary>
        private void LoadAchievements()
        {
            if (_achievementsLoaded)
                return;

            var achievementsFilePath = "Data/Achievements/achievements.json";

            try
            {
                var json = File.ReadAllText(achievementsFilePath);
                var loadedAchievements = JsonConvert.DeserializeObject<List<AchievementDefinition>>(
                    json
                );
                Log.Verbose(
                    "Loaded Achievements: {Json}",
                    JsonConvert.SerializeObject(loadedAchievements, Formatting.Indented)
                );

                if (loadedAchievements != null)
                {
                    foreach (var achievement in loadedAchievements)
                    {
                        _achievements[achievement.Id] = achievement;
                    }
                    Log.Information(
                        "Successfully loaded {Count} achievements.",
                        _achievements.Count
                    );
                }
                else
                {
                    Log.Warning("No achievements found in the JSON file.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading achievements from file.");
            }

            _achievementsLoaded = true;
        }

        /// <summary>
        /// Returns a list of all available achievements.
        /// </summary>
        /// <returns>List of achievement definitions</returns>
        public List<AchievementDefinition> GetAllAchievements() => _achievements.Values.ToList();

        /// <summary>
        /// Retrieves a specific achievement by its ID.
        /// </summary>
        /// <param name="achievementId">The unique identifier of the achievement</param>
        /// <returns>The achievement definition if found; otherwise null</returns>
        public AchievementDefinition? GetAchievement(string achievementId)
        {
            _achievements.TryGetValue(achievementId, out var achievement);
            return achievement;
        }

        /// <summary>
        /// Checks if a user meets the requirements for a specific achievement.
        /// An achievement unlocks when ANY of its requirements are met, not all of them.
        /// </summary>
        /// <param name="user">The user information to check</param>
        /// <param name="achievement">The achievement definition to validate against</param>
        /// <returns>True if the user meets any of the requirements; otherwise false</returns>
        public static bool MeetsRequirements(
            UserInformation user,
            AchievementDefinition achievement
        )
        {
            var countingData = user.CountingData.Values;

            int totalCounts = countingData.Sum(c => c.TotalCounts);
            int totalCorrect = countingData.Sum(c => c.TotalCorrectCounts);
            int bestStreak = countingData.Max(c => c.BestStreak);
            int incorrectToday = user.IncorrectCountsToday;
            int level = user.Level;
            int coins = user.Coins;
            int activeDays = user.ActiveDaysThisWeek;
            double accountAge = (DateTime.UtcNow - user.FirstCountTime).TotalDays;
            Log.Debug("Account Age {AccountAge}", accountAge);
            int hourNow = DateTime.UtcNow.Hour;

            var req = achievement.Requirements;

            return (req.MinTotalCounts.HasValue && totalCounts >= req.MinTotalCounts.Value)
                || (req.MinCorrectCounts.HasValue && totalCorrect >= req.MinCorrectCounts.Value)
                || (req.MinBestStreak.HasValue && bestStreak >= req.MinBestStreak.Value)
                || (
                    req.MinStreakAfterError.HasValue
                    && incorrectToday > 0
                    && bestStreak > req.MinStreakAfterError.Value
                )
                || (req.MinLevel.HasValue && level >= req.MinLevel.Value)
                || (req.MinCoins.HasValue && coins >= req.MinCoins.Value)
                || (req.ActiveDaysThisWeek.HasValue && activeDays >= req.ActiveDaysThisWeek.Value)
                || (req.AccountAgeDays.HasValue && accountAge >= req.AccountAgeDays.Value)
                || (
                    req.UtcHourRange is { Length: 2 }
                    && IsWithinUtcHourRange(req.UtcHourRange, hourNow)
                );
        }

        /// <summary>
        /// Checks if the current UTC hour falls within the specified range.
        /// Handles ranges that cross midnight (e.g., 22-04).
        /// </summary>
        /// <param name="utcHourRange">Array containing start and end hours</param>
        /// <param name="hourNow">Current UTC hour</param>
        /// <returns>True if current hour is within range; otherwise false</returns>
        private static bool IsWithinUtcHourRange(int[] utcHourRange, int hourNow)
        {
            int start = utcHourRange[0];
            int end = utcHourRange[1];
            return start <= end
                ? hourNow >= start && hourNow < end
                : hourNow >= start || hourNow < end;
        }

        /// <summary>
        /// Attempts to unlock any achievements that the user has newly qualified for.
        /// </summary>
        /// <param name="userId">The ID of the user to check</param>
        /// <returns>True if any new achievements were unlocked; otherwise false</returns>
        public async Task<bool> TryUnlock(ulong userId)
        {
            var user = await _dbContext.UserInformation.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("User with ID {UserId} not found.", userId);
                return false;
            }

            var unlockedAchievementIds = user
                .UnlockedAchievements.Select(a => a.AchievementId)
                .ToHashSet();
            var notUnlockedAchievements = _achievements
                .Values.Where(a => !unlockedAchievementIds.Contains(a.Id))
                .ToList();

            return notUnlockedAchievements
                    .Where(achievement => MeetsRequirements(user, achievement))
                    .Select(achievement =>
                    {
                        var updatedAchievements = user.UnlockedAchievements.ToList();
                        updatedAchievements.Add(
                            new UnlockedAchievementData
                            {
                                AchievementId = achievement.Id,
                                Name = achievement.Name,
                                Description = achievement.Description!,
                            }
                        );

                        user.UnlockedAchievements = updatedAchievements;
                        return _dbContext.SaveChangesAsync();
                    })
                    .FirstOrDefault() != null;
        }
    }

    /// <summary>
    /// Represents the definition of an achievement, including its requirements and metadata.
    /// </summary>
    public class AchievementDefinition
    {
        /// <summary>
        /// Unique identifier for the achievement
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the achievement
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of how to earn the achievement
        /// </summary>
        public string? Description { get; set; } = string.Empty;

        /// <summary>
        /// Category of the achievement
        /// </summary>
        public AchievementType Type { get; set; } = AchievementType.Milestone;

        /// <summary>
        /// Specific criteria that must be met to earn the achievement
        /// </summary>
        public AchievementRequirements Requirements { get; set; } =
            new() { UtcHourRange = Array.Empty<int>() };

        /// <summary>
        /// Whether the achievement should be hidden until unlocked
        /// </summary>
        public bool Secret { get; set; } = false;

        /// <summary>
        /// Whether to show progress towards completing the achievement
        /// </summary>
        public bool TrackProgress { get; set; } = false;

        /// <summary>
        /// Whether the achievement has been completed
        /// </summary>
        public bool IsCompleted { get; set; } = false;
    }

    /// <summary>
    /// Categorizes achievements into different types
    /// </summary>
    public enum AchievementType
    {
        Milestone, // Progress-based achievements
        Skill, // Achievements based on user ability
        TimeBased, // Achievements tied to specific times
        Collection, // Collecting or completing sets
        Challenge, // Special challenge achievements
    }

    /// <summary>
    /// Defines the specific criteria that must be met to earn an achievement
    /// </summary>
    public class AchievementRequirements
    {
        public int? MinTotalCounts { get; set; }
        public int? MinCorrectCounts { get; set; }
        public int? MinBestStreak { get; set; }
        public int? MinStreakAfterError { get; set; }
        public int? MinLevel { get; set; }
        public int? MinCoins { get; set; }
        public int? MinChallengesCompleted { get; set; }
        public bool? PerfectDay { get; set; }
        public int? ActiveDaysThisWeek { get; set; }
        public required int[] UtcHourRange { get; set; }
        public int? AccountAgeDays { get; set; }
    }
}
