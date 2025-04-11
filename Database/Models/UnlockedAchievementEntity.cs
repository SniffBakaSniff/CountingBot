using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CountingBot.Database.Models
{
    /// <summary>
    /// Entity for storing unlocked achievements in a normalized table.
    /// </summary>
    public class UnlockedAchievementEntity
    {
        [Key]
        public int Id { get; set; }

        public ulong UserId { get; set; }
        public string AchievementId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsCompleted { get; set; }
        public bool Secret { get; set; }
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public UserInformation? User { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Creates an UnlockedAchievementEntity from an UnlockedAchievementData object.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="data">The achievement data.</param>
        /// <param name="isCompleted">Whether the achievement is completed.</param>
        /// <param name="secret">Whether the achievement is secret.</param>
        /// <returns>A new UnlockedAchievementEntity.</returns>
        public static UnlockedAchievementEntity FromUnlockedAchievementData(
            ulong userId,
            UnlockedAchievementData data,
            bool isCompleted = true,
            bool secret = false
        )
        {
            return new UnlockedAchievementEntity
            {
                UserId = userId,
                AchievementId = data.AchievementId,
                Name = data.Name,
                Description = data.Description,
                IsCompleted = isCompleted,
                Secret = secret,
                UnlockedAt = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Converts this entity to an UnlockedAchievementData object.
        /// </summary>
        /// <returns>An UnlockedAchievementData object.</returns>
        public UnlockedAchievementData ToUnlockedAchievementData()
        {
            return new UnlockedAchievementData
            {
                AchievementId = AchievementId,
                Name = Name,
                Description = Description!,
            };
        }
    }
}
