using Newtonsoft.Json;
using CountingBot.Database.Models;
using Serilog;
using Microsoft.EntityFrameworkCore;
using CountingBot.Database;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Services
{
    public class AchievementService
    {
        private readonly Dictionary<string, AchievementDefinition> _achievements = new();
        private readonly BotDbContext _dbContext;
        private bool _achievementsLoaded = false;

        public AchievementService(BotDbContext dbContext)
        {
            LoadAchievements();
            _dbContext = dbContext;
        }

        private void LoadAchievements()
        {
            if (_achievementsLoaded)
                return;

            var achievementsFilePath = "Data/Achievements/achievements.json";

            try
            {
                var json = File.ReadAllText(achievementsFilePath);

                var loadedAchievements = JsonConvert.DeserializeObject<List<AchievementDefinition>>(json);
                Log.Verbose("Loaded Achievements: {Json}", JsonConvert.SerializeObject(loadedAchievements, Formatting.Indented));

                if (loadedAchievements != null)
                {
                    foreach (var achievement in loadedAchievements)
                    {
                        _achievements[achievement.Id] = achievement;
                    }

                    Log.Information("Successfully loaded {Count} achievements.", _achievements.Count);
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

        public List<AchievementDefinition> GetAllAchievements() => _achievements.Values.ToList();

        public AchievementDefinition? GetAchievement(string achievementId)
        {
            _achievements.TryGetValue(achievementId, out var achievement);
            return achievement;
        }

        public static bool MeetsRequirements(UserInformation user, AchievementDefinition achievement)
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

            // Check the requirements for the specific achievement
            if ((req.MinTotalCounts.HasValue && totalCounts >= req.MinTotalCounts.Value) ||
                (req.MinCorrectCounts.HasValue && totalCorrect >= req.MinCorrectCounts.Value) ||
                (req.MinBestStreak.HasValue && bestStreak >= req.MinBestStreak.Value) ||
                (req.MinStreakAfterError.HasValue && incorrectToday > 0 && bestStreak > req.MinStreakAfterError.Value) ||
                (req.MinLevel.HasValue && level >= req.MinLevel.Value) ||
                (req.MinCoins.HasValue && coins >= req.MinCoins.Value) ||
                (req.ActiveDaysThisWeek.HasValue && activeDays >= req.ActiveDaysThisWeek.Value) ||
                (req.AccountAgeDays.HasValue && accountAge >= req.AccountAgeDays.Value) ||
                (req.UtcHourRange is { Length: 2 } && IsWithinUtcHourRange(req.UtcHourRange, hourNow)))
            {
                return true;
            }

            return false;
        }

        private static bool IsWithinUtcHourRange(int[] utcHourRange, int hourNow)
        {
            int start = utcHourRange[0];
            int end = utcHourRange[1];
            return start <= end
                ? hourNow >= start && hourNow < end
                : hourNow >= start || hourNow < end;
        }


        public async Task<bool> TryUnlock(ulong userId)
        {
            var user = await _dbContext.UserInformation.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("User with ID {UserId} not found.", userId);
                return false;
            }

            var unlockedAchievementIds = user.UnlockedAchievements.Select(a => a.AchievementId).ToHashSet();
            var notUnlockedAchievements = _achievements.Values
                .Where(a => !unlockedAchievementIds.Contains(a.Id))
                .ToList();

            foreach (var achievement in notUnlockedAchievements)
            {
                if (MeetsRequirements(user, achievement))
                {
                    var updatedAchievements = user.UnlockedAchievements.ToList();
                    updatedAchievements.Add(new UnlockedAchievementData
                    {
                        AchievementId = achievement.Id,
                        Name = achievement.Name,
                        Description = achievement.Description!
                    });

                    user.UnlockedAchievements = updatedAchievements;
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
            }

            return false;
        }
    }

    public class AchievementDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public AchievementType Type { get; set; } = AchievementType.Milestone;
        public AchievementRequirements Requirements { get; set; } = new();
        public bool Secret { get; set; } = false;
        public bool TrackProgress { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
    }

    public enum AchievementType
    {
        Milestone,
        Skill,
        TimeBased,
        Collection,
        Challenge
    }

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
        public int[] UtcHourRange { get; set; }
        public int? AccountAgeDays { get; set; }
    }
}