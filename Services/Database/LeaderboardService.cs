using CountingBot.Database;
using CountingBot.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CountingBot.Services.Database
{
    /// <summary>
    /// Service implementation for managing and retrieving leaderboard data.
    /// Handles both guild-specific and global leaderboard statistics.
    /// </summary>
    public class LeaderboardService : ILeaderboardService
    {
        /// <summary>
        /// Retrieves multiple leaderboards with pagination support for both guild-specific and global statistics.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve (1-based indexing)</param>
        /// <param name="pageSize">Number of entries per page</param>
        /// <param name="guildId">Discord guild ID to filter results for guild-specific leaderboards</param>
        /// <returns>Dictionary containing different leaderboard categories with their respective user lists</returns>
        public async Task<Dictionary<string, List<UserInformation>>> GetLeaderboardsAsync(
            int pageNumber,
            int pageSize,
            ulong guildId
        )
        {
            using var dbContext = new BotDbContext();
            var skip = (pageNumber - 1) * pageSize;

            var usersInfo = await dbContext.UserInformation.ToListAsync();
            var filteredUsers = usersInfo
                .Where(ui => ui.CountingData.ContainsKey(guildId))
                .ToList();

            if (!filteredUsers.Any())
            {
                Log.Warning("No users have counting data for guild {GuildId}.", guildId);
                return new Dictionary<string, List<UserInformation>>();
            }

            Log.Information("Found users with counting data for guild {GuildId}.", guildId);

            var leaderboards = new Dictionary<string, List<UserInformation>>
            {
                // Guild-specific leaderboards
                ["TotalCounts"] = filteredUsers
                    .OrderByDescending(u => u.CountingData[guildId].TotalCounts)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["HighestCount"] = filteredUsers
                    .OrderByDescending(u => u.CountingData[guildId].HighestCount)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["TotalCorrectCounts"] = filteredUsers
                    .OrderByDescending(u => u.CountingData[guildId].TotalCorrectCounts)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["CurrentStreak"] = filteredUsers
                    .OrderByDescending(u => u.CountingData[guildId].CurrentStreak)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["BestStreak"] = filteredUsers
                    .OrderByDescending(u => u.CountingData[guildId].BestStreak)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                // Global leaderboards
                ["GlobalTotalCounts"] = usersInfo
                    .OrderByDescending(u => u.CountingData.Sum(c => c.Value.TotalCounts))
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["GlobalHighestCount"] = usersInfo
                    .OrderByDescending(u => u.CountingData.Sum(c => c.Value.HighestCount))
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["GlobalTotalCorrectCounts"] = usersInfo
                    .OrderByDescending(u => u.CountingData.Sum(c => c.Value.TotalCorrectCounts))
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["GlobalCurrentStreak"] = usersInfo
                    .OrderByDescending(u => u.CountingData.Sum(c => c.Value.CurrentStreak))
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),

                ["GlobalBestStreak"] = usersInfo
                    .OrderByDescending(u => u.CountingData.Sum(c => c.Value.BestStreak))
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),
            };

            foreach (var leaderboard in leaderboards)
            {
                Log.Information(
                    "Fetched page {PageNumber} of {Leaderboard} leaderboard for guild {GuildId}.",
                    pageNumber,
                    leaderboard.Key,
                    guildId
                );
            }

            return leaderboards;
        }
    }
}
