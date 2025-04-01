using System.ComponentModel.DataAnnotations;

namespace CountingBot.Database
{
    public class CountingStats
    {
        public int TotalCount { get; set; }
        public int HighestCount { get; set; }
        public int TotalCorrectCounts { get; set; }
        public int TotalIncorrectCounts { get; set; }
        public int ErrorCount { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
    }

    public class UserInformation
    {
        [Key]
        public ulong UserId { get; set; }
        public Dictionary<ulong, CountingStats> CountingData { get; set; } = new Dictionary<ulong, CountingStats>();
        public int Coins { get; set; }
        public int ExperiencePoints { get; set; }
        public int Level { get; set; }
        public int Revives { get; set; }
        public int RevivesUsed { get; set; }
        public DateTime FirstCountTime { get; set; } = DateTime.UtcNow;
        public DateTime LastCountTime { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalTimeCounting { get; set; } = TimeSpan.Zero;
        public int ChallengesCompleted { get; set; }
        public int AchievementsUnlocked { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

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