using System.ComponentModel.DataAnnotations;

namespace CountingBot.Database.Models
{
    public class CountingStats
    {
        public int TotalCounts { get; set; }
        public int TotalCorrectCounts { get; set; }
        public int TotalIncorrectCounts { get; set; }
        public int HighestCount { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
    }

    public class UnlockedAchievementData
    {
        public string AchievementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UserInformation
    {
        [Key]
        public ulong UserId { get; set; }

        // Preferences
        public string PreferredLanguage { get; set; } = "en";

        // Counting stats per guild
        public Dictionary<ulong, CountingStats> CountingData { get; set; } = [];

        // Achievements
        public List<UnlockedAchievementData> UnlockedAchievements { get; set; } = [];
        public int AchievementsUnlocked { get; set; }
        public int ChallengesCompleted { get; set; }

        // Progression
        public int Level { get; set; }
        public int ExperiencePoints { get; set; }
        public int TotalExperiencePoints { get; set; }

        // Coins
        public int Coins { get; set; }

        // Revives
        public int Revives { get; set; }
        public int RevivesUsed { get; set; }

        // Activity tracking
        public DateTime CurrentDay { get; set; } = DateTime.UtcNow;
        public DateTime CurrentWeek { get; set; } = DateTime.UtcNow;
        public int ActiveDaysThisWeek { get; set; }
        public int TimesCountedToday { get; set; }
        public int IncorrectCountsToday { get; set;}
        public int CorrectCountsToday { get; set; }

        // Timestamps
        public DateTime FirstCountTime { get; set; } = DateTime.UtcNow;
        public DateTime LastCountTime { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Concurrency
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public CountingStats GetOrCreateCountingStats(ulong guildId)
        {
            if (!CountingData.TryGetValue(guildId, out var stats))
            {
                stats = new CountingStats();
                CountingData[guildId] = stats;
            }
            return stats;
        }
    }
}
