using Serilog;

using CountingBot.Database;
using CountingBot.Database.Models;
using DSharpPlus.Entities;
using System.Data;

namespace CountingBot.Services.Database
{
    public class UserInformationService : IUserInformationService
    {
        private readonly AchievementService _achievementService;
        private readonly BotDbContext _dbContext;

        public UserInformationService(AchievementService achievementService, BotDbContext dbContext)
        {
            _achievementService = achievementService;
            _dbContext = dbContext;
        }

        public async Task<UserInformation> GetUserInformationAsync(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                Log.Information("Fetching user info for user {UserId}", userId);
                var userInformation = await GetOrCreateUserInformationAsync(userId)
                    .ConfigureAwait(false);
                return userInformation;
            }).ConfigureAwait(false);
        }

        public async Task UpdateUserCountAsync(ulong guildId, ulong userId, int currentCount, bool correctCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {

                Log.Information("Updating total count for user {UserId} in guild {GuildId}", userId, guildId);
                var userInformation = await GetOrCreateUserInformationAsync(userId)
                    .ConfigureAwait(false);

                var stats = userInformation.GetOrCreateCountingStats(guildId);
                var random = new Random();

                stats.TotalCounts++;

                if (correctCount)
                {
                    stats.TotalCorrectCounts++;
                    stats.CurrentStreak++;
                    int earnedXp = random.Next(1, 6);
                    userInformation.ExperiencePoints += earnedXp;
                    userInformation.TotalExperiencePoints += earnedXp;
                    if (currentCount >= stats.HighestCount)
                    {
                        stats.HighestCount = currentCount;
                    }
                    if(userInformation.ExperiencePoints >= userInformation.Level * 100)
                    {
                        userInformation.Level++;
                        userInformation.ExperiencePoints = 0;
                    }
                    if (userInformation.Revives < 3 && stats.CurrentStreak % 500 == 0)
                    {
                        userInformation.Revives++;
                    }
                    var unlocked = await _achievementService.TryUnlock(userInformation.UserId);
                    if (unlocked)
                    {
                        Log.Information("Unlocked Achievements");
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

                _dbContext.Entry(userInformation)
                    .Property(u => u.CountingData)
                    .IsModified = true;

                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Successfully updated counting stats for user {UserId} in guild {GuildId}", userId, guildId);
            }).ConfigureAwait(false);
        }

        public async Task<bool> GetUserRevivesAsync(ulong userId, bool removeRevive)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                Log.Information("Fetching user info for user {UserId}", userId);
                
                var userInformation = await GetOrCreateUserInformationAsync(userId)
                    .ConfigureAwait(false);

                if (userInformation.Revives > 0)
                {
                    if (removeRevive)
                    {
                        userInformation.Revives--;
                        userInformation.RevivesUsed++;
                        Log.Information("User {UserId} used a revive. Remaining: {Revives}", 
                                        userId, userInformation.Revives);

                        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                    }
                    return true;
                }

                return false;
            }).ConfigureAwait(false);
        }

        public async Task<string> GetUserPreferredLanguageAsync(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {

                Log.Information("Fetching preferred language for user {UserId}.", userId);

                var userInfo = await _dbContext.UserInformation.FindAsync(userId).ConfigureAwait(false);

                if (userInfo is null)
                {
                    Log.Warning("No user information found for user {UserId}. Returning default language 'en'.", userId);
                    return "en";
                }

                Log.Information("Preferred language for user {UserId} is '{PreferredLanguage}'.", userId, userInfo.PreferredLanguage);
                return userInfo.PreferredLanguage ?? "en";
            }).ConfigureAwait(false);
        }
        public async Task SetPreferredLanguageAsync(ulong userId, string language)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {

                Log.Information("Setting preferred language for user {UserId}.", userId);

                var userInfo = await _dbContext.UserInformation.FindAsync(userId).ConfigureAwait(false);

                if (userInfo is null)
                {
                    Log.Warning("No user information found for user {UserId}. Cannot set preferred language.", userId);
                    return;
                }

                userInfo.PreferredLanguage = language;

                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Successfully set preferred language for user {UserId} to '{PreferredLanguage}'.", userId, language);
            }).ConfigureAwait(false);
        }

        public async Task<List<AchievementDefinition>> GetUnlockedAchievementsAsync(ulong userId, int pageNumber = 1, int pageSize = 10)
        {
            var userInfo = await _dbContext.UserInformation.FindAsync(userId);
            
            if (userInfo == null)
            {
                return new List<AchievementDefinition>();
            }

            var unlockedAchievementIds = userInfo.UnlockedAchievements.Select(a => a.AchievementId).ToList();
            var allAchievements = _achievementService.GetAllAchievements();

            var paginatedAchievements = allAchievements
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AchievementDefinition
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    Type = a.Type,
                    Requirements = a.Requirements,
                    Secret = a.Secret,
                    TrackProgress = a.TrackProgress,
                    IsCompleted = unlockedAchievementIds.Contains(a.Id)
                })
                .ToList();

            return paginatedAchievements;
        }

        public async Task<DateTime> GetOrUpdateCurrentDay(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                var userInfo = await _dbContext.UserInformation.FindAsync(userId);
                var currentDay = DateTime.UtcNow;
                var currentWeek = GetStartOfWeek(currentDay);

                if (userInfo is null)
                {
                    Log.Warning("No user information found for user {UserId}. Cannot set preferred language.", userId);
                    throw new InvalidOperationException($"No user information found for user {userId}.");
                }

                if (userInfo.CurrentDay.Day > currentDay.Day)
                {
                    userInfo.CurrentDay = userInfo.CurrentDay.AddDays(1);
                }
                else
                {
                    userInfo.CurrentDay = currentDay;
                }

                if (userInfo.CurrentWeek != currentWeek)
                {
                    userInfo.CurrentWeek = currentWeek;
                    userInfo.ActiveDaysThisWeek = 1;
                }
                else
                {
                    userInfo.ActiveDaysThisWeek++;
                }

                await _dbContext.SaveChangesAsync();
                return userInfo.CurrentDay;
            });
        }

        private static DateTime GetStartOfWeek(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;
            var diff = dayOfWeek - DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;
            return date.AddDays(-diff).Date;
        }


        public async Task UpdateIncorrectCountsToday(ulong userId)
        {
            await ExceptionHandler.HandleAsync(async () => 
            {
                var userInfo = await _dbContext.UserInformation.FindAsync(userId);
                DateTime currentDay = DateTime.UtcNow;
                
                if (userInfo is null)
                {
                    throw new InvalidOperationException($"No user information found for user {userId}.");
                }
                
                if (userInfo.CurrentDay.Day == currentDay.Day)
                {
                    userInfo.IncorrectCountsToday++;
                }
                else
                {
                    userInfo.IncorrectCountsToday = 1;
                }
            });
        }

        public async Task UpdateCorrectCountsToday(ulong userId)
        {
            await ExceptionHandler.HandleAsync(async () => 
            {
                var userInfo = await _dbContext.UserInformation.FindAsync(userId);
                DateTime currentDay = DateTime.UtcNow;
                
                if (userInfo is null)
                {
                    throw new InvalidOperationException($"No user information found for user {userId}.");
                }
                
                if (userInfo.CurrentDay.Day == currentDay.Day)
                {
                    userInfo.CorrectCountsToday++;
                }
                else
                {
                    userInfo.CorrectCountsToday = 1;
                }
            });
        }

        public async Task DeleteUserInformationAsync(ulong userId)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {

                Log.Information("Reseting User {UserId} User Information");
                var userInfo = await _dbContext.UserInformation.FindAsync(userId).ConfigureAwait(false);
                if (userInfo is not null)
                    _dbContext.UserInformation.Remove(userInfo);
                await _dbContext.SaveChangesAsync();
            });
        }

        private async Task<UserInformation> GetOrCreateUserInformationAsync(ulong userId)
        {
            var userInformation = await _dbContext.UserInformation.FindAsync(userId)
                .ConfigureAwait(false);

            if (userInformation == null)
            {
                Log.Information("User information not found for {UserId}, creating new user entry.", userId);
                userInformation = new UserInformation { UserId = userId, FirstCountTime = DateTime.UtcNow };
                _dbContext.UserInformation.Add(userInformation);
            }

            return userInformation;
        }
    }
}