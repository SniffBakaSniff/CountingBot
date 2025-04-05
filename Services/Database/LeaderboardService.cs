using Serilog;
using CountingBot.Database;
using CountingBot.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace CountingBot.Services.Database
{
    public class LeaderboardService : ILeaderboardService
    {
        public async Task<Dictionary<string, List<UserInformation>>> GetLeaderboardsAsync(int pageNumber, int pageSize, ulong guildId)
        {
            using var dbContext = new BotDbContext();
            var skip = (pageNumber - 1) * pageSize;

            var usersInfo = await dbContext.UserInformation.ToListAsync();
            var filteredUsers = usersInfo.Where(ui => ui.CountingData.ContainsKey(guildId)).ToList();

            if (!filteredUsers.Any())
            {
                Log.Warning("No users have counting data for guild {GuildId}.", guildId);
                return new Dictionary<string, List<UserInformation>>();
            }

            Log.Information("Found users with counting data for guild {GuildId}.", guildId);

            var leaderboards = new Dictionary<string, List<UserInformation>>();

            leaderboards["TotalCounts"] = filteredUsers
                .OrderByDescending(u => u.CountingData[guildId].TotalCounts)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            leaderboards["HighestCount"] = filteredUsers
                .OrderByDescending(u => u.CountingData[guildId].HighestCount)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            leaderboards["TotalCorrectCounts"] = filteredUsers
                .OrderByDescending(u => u.CountingData[guildId].TotalCorrectCounts)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            leaderboards["CurrentStreak"] = filteredUsers
                .OrderByDescending(u => u.CountingData[guildId].CurrentStreak)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            leaderboards["BestStreak"] = filteredUsers
                .OrderByDescending(u => u.CountingData[guildId].BestStreak)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            var globalLeaderboards = new Dictionary<string, List<UserInformation>>();

            globalLeaderboards["TotalCounts"] = usersInfo
                .OrderByDescending(u => u.CountingData.Sum(c => c.Value.TotalCounts))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            globalLeaderboards["HighestCount"] = usersInfo
                .OrderByDescending(u => u.CountingData.Sum(c => c.Value.HighestCount))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            globalLeaderboards["TotalCorrectCounts"] = usersInfo
                .OrderByDescending(u => u.CountingData.Sum(c => c.Value.TotalCorrectCounts))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            globalLeaderboards["CurrentStreak"] = usersInfo
                .OrderByDescending(u => u.CountingData.Sum(c => c.Value.CurrentStreak))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            globalLeaderboards["BestStreak"] = usersInfo
                .OrderByDescending(u => u.CountingData.Sum(c => c.Value.BestStreak))
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            foreach (var leaderboard in leaderboards)
            {
                Log.Information("Fetched page {PageNumber} of {Leaderboard} leaderboard for guild {GuildId}.", pageNumber, leaderboard.Key, guildId);
            }

            foreach (var leaderboard in globalLeaderboards)
            {
                Log.Information("Fetched page {PageNumber} of global {Leaderboard} leaderboard.", pageNumber, leaderboard.Key);
            }

            leaderboards.Add("GlobalTotalCounts", globalLeaderboards["TotalCounts"]);
            leaderboards.Add("GlobalHighestCount", globalLeaderboards["HighestCount"]);
            leaderboards.Add("GlobalTotalCorrectCounts", globalLeaderboards["TotalCorrectCounts"]);
            leaderboards.Add("GlobalCurrentStreak", globalLeaderboards["CurrentStreak"]);
            leaderboards.Add("GlobalBestStreak", globalLeaderboards["BestStreak"]);

            return leaderboards;
        }
    }
}