using Serilog;

using CountingBot.Database;
using CountingBot.Database.Models;

namespace CountingBot.Services.Database
{
    public class UserInformationService : IUserInformationService
    {
        public async Task<UserInformation> GetUserInformationAsync(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching user info for user {UserId}", userId);
                var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId)
                    .ConfigureAwait(false);
                return userInformation;
            }).ConfigureAwait(false);
        }

        public async Task UpdateUserCountAsync(ulong guildId, ulong userId, int currentCount, bool correctCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Updating total count for user {UserId} in guild {GuildId}", userId, guildId);

                var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId)
                    .ConfigureAwait(false);

                var stats = userInformation.GetOrCreateCountingStats(guildId);

                stats.TotalCounts++;

                if (correctCount)
                {
                    stats.TotalCorrectCounts++;
                    stats.CurrentStreak++;
                    if (currentCount >= stats.HighestCount)
                    {
                        stats.HighestCount = currentCount;
                    }
                }
                else
                {
                    stats.TotalIncorrectCounts++;
                    stats.CurrentStreak = 0;
                }

                if (stats.CurrentStreak > stats.BestStreak)
                {
                    stats.BestStreak = stats.CurrentStreak;
                }

                userInformation.LastUpdated = DateTime.UtcNow;

                dbContext.Entry(userInformation)
                    .Property(u => u.CountingData)
                    .IsModified = true;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Successfully updated counting stats for user {UserId} in guild {GuildId}", userId, guildId);
            }).ConfigureAwait(false);
        }

        public async Task<bool> GetUserRevivesAsync(ulong userId, bool removeRevive)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                await using var dbContext = new BotDbContext();
                Log.Information("Fetching user info for user {UserId}", userId);
                
                var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId)
                    .ConfigureAwait(false);

                if (userInformation.Revives > 0)
                {
                    if (removeRevive)
                    {
                        userInformation.Revives--;
                        Log.Information("User {UserId} used a revive. Remaining: {Revives}", 
                                        userId, userInformation.Revives);

                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    }
                    return true;
                }

                return false;
            }).ConfigureAwait(false);
        }


        private async Task<UserInformation> GetOrCreateUserInformationAsync(BotDbContext dbContext, ulong userId)
        {
            var userInformation = await dbContext.UserInformation.FindAsync(userId)
                .ConfigureAwait(false);

            if (userInformation == null)
            {
                Log.Information("User information not found for {UserId}, creating new user entry.", userId);
                userInformation = new UserInformation { UserId = userId };
                dbContext.UserInformation.Add(userInformation);
            }

            return userInformation;
        }
    }
}