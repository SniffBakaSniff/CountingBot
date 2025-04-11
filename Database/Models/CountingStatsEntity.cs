using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CountingBot.Database.Models
{
    /// <summary>
    /// Entity for storing counting statistics in a normalized table.
    /// </summary>
    public class CountingStatsEntity
    {
        [Key]
        public int Id { get; set; }

        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }

        public int TotalCounts { get; set; }
        public int TotalCorrectCounts { get; set; }
        public int TotalIncorrectCounts { get; set; }
        public int HighestCount { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }

        [ForeignKey("UserId")]
        public UserInformation? User { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Creates a CountingStatsEntity from a CountingStats object.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="guildId">The guild ID.</param>
        /// <param name="stats">The counting stats.</param>
        /// <returns>A new CountingStatsEntity.</returns>
        public static CountingStatsEntity FromCountingStats(
            ulong userId,
            ulong guildId,
            CountingStats stats
        )
        {
            return new CountingStatsEntity
            {
                UserId = userId,
                GuildId = guildId,
                TotalCounts = stats.TotalCounts,
                TotalCorrectCounts = stats.TotalCorrectCounts,
                TotalIncorrectCounts = stats.TotalIncorrectCounts,
                HighestCount = stats.HighestCount,
                CurrentStreak = stats.CurrentStreak,
                BestStreak = stats.BestStreak,
                LastUpdated = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Converts this entity to a CountingStats object.
        /// </summary>
        /// <returns>A CountingStats object.</returns>
        public CountingStats ToCountingStats()
        {
            return new CountingStats
            {
                TotalCounts = TotalCounts,
                TotalCorrectCounts = TotalCorrectCounts,
                TotalIncorrectCounts = TotalIncorrectCounts,
                HighestCount = HighestCount,
                CurrentStreak = CurrentStreak,
                BestStreak = BestStreak,
            };
        }
    }
}
