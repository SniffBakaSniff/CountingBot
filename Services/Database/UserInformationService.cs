using System.Data;
using CountingBot.Database;
using CountingBot.Database.Models;
using Serilog;

namespace CountingBot.Services.Database
{
    /// <summary>
    /// Service for managing user information and statistics in the database.
    /// </summary>
    public class UserInformationService : IUserInformationService
    {
        private readonly AchievementService _achievementService;
        private readonly ICacheService? _cacheService;
        private const string CacheKeyPrefix = CacheService.UserInfoPrefix;

        /// <summary>
        /// Initializes a new instance of the UserInformationService with caching support.
        /// </summary>
        /// <param name="achievementService">Service for managing user achievements.</param>
        /// <param name="cacheService">The cache service to use.</param>
        public UserInformationService(
            AchievementService achievementService,
            ICacheService cacheService
        )
        {
            _achievementService = achievementService;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Retrieves or creates user information for the specified user.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <returns>User information object.</returns>
        public async Task<UserInformation> GetUserInformationAsync(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                string cacheKey = $"{CacheKeyPrefix}{userId}";

                // Try to get from cache first
                if (
                    _cacheService != null
                    && _cacheService.TryGetValue<UserInformation>(cacheKey, out var cachedUserInfo)
                    && cachedUserInfo != null
                )
                {
                    Log.Debug("Retrieved user info for user {UserId} from cache", userId);
                    return cachedUserInfo;
                }

                // Not in cache, get from database
                Log.Information("Fetching user info for user {UserId} from database", userId);

                // Create a database context
                var dbContext = new BotDbContext();

                try
                {
                    var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId);

                    // Store in cache for future requests with appropriate expiration
                    _cacheService?.Set(cacheKey, userInformation);

                    return userInformation;
                }
                finally
                {
                    await dbContext.DisposeAsync();
                }
            });
        }

        /// <summary>
        /// Updates user's counting statistics.
        /// </summary>
        /// <param name="guildId">Discord guild ID.</param>
        /// <param name="userId">Discord user ID.</param>
        /// <param name="currentCount">Current count value.</param>
        /// <param name="correctCount">Whether the count was correct.</param>
        public async Task UpdateUserCountAsync(
            ulong guildId,
            ulong userId,
            int currentCount,
            bool correctCount
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                // Create a database context
                var dbContext = new BotDbContext();

                try
                {
                    Log.Information(
                        "Updating total count for user {UserId} in guild {GuildId}",
                        userId,
                        guildId
                    );
                    var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId);

                    // Invalidate cache since we're updating user data
                    string cacheKey = $"{CacheKeyPrefix}{userId}";
                    _cacheService?.Remove(cacheKey);

                    // Update the user's counting statistics
                    await UpdateUserCountingStatsAsync(
                        userInformation,
                        guildId,
                        currentCount,
                        correctCount
                    );

                    // Save changes to the database
                    dbContext.Entry(userInformation).Property(u => u.CountingData).IsModified =
                        true;

                    await dbContext.SaveChangesAsync();
                    Log.Information(
                        "Successfully updated counting stats for user {UserId} in guild {GuildId}",
                        userId,
                        guildId
                    );
                }
                finally
                {
                    await dbContext.DisposeAsync();
                }
            });
        }

        /// <summary>
        /// Updates the user's counting statistics.
        /// </summary>
        /// <param name="userInformation">The user information object.</param>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="currentCount">The current count value.</param>
        /// <param name="correctCount">Whether the count was correct.</param>
        private async Task UpdateUserCountingStatsAsync(
            UserInformation userInformation,
            ulong guildId,
            int currentCount,
            bool correctCount
        )
        {
            var stats = userInformation.GetOrCreateCountingStats(guildId);
            var random = new Random();

            stats.TotalCounts++;

            if (correctCount)
            {
                await UpdateCorrectCountStatsAsync(userInformation, stats, currentCount, random);
                await UpdateCorrectCountsToday(userInformation);
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
        }

        /// <summary>
        /// Updates the user's statistics for a correct count.
        /// </summary>
        /// <param name="userInformation">The user information object.</param>
        /// <param name="stats">The counting statistics.</param>
        /// <param name="currentCount">The current count value.</param>
        /// <param name="random">Random number generator.</param>
        private async Task UpdateCorrectCountStatsAsync(
            UserInformation userInformation,
            CountingStats stats,
            int currentCount,
            Random random
        )
        {
            stats.TotalCorrectCounts++;
            stats.CurrentStreak++;

            // Award XP
            int earnedXp = random.Next(1, 6);
            userInformation.ExperiencePoints += earnedXp;
            userInformation.TotalExperiencePoints += earnedXp;

            // Update highest count if needed
            if (currentCount >= stats.HighestCount)
            {
                stats.HighestCount = currentCount;
            }

            // Level up if enough XP
            if (userInformation.ExperiencePoints >= userInformation.Level * 100)
            {
                userInformation.Level++;
                userInformation.ExperiencePoints = 0;
            }

            // Award revive token at milestone streaks
            if (userInformation.Revives < 1 && stats.CurrentStreak % 500 == 0)
            {
                userInformation.Revives++;
            }

            // Check for achievements
            var unlocked = await _achievementService.TryUnlock(userInformation.UserId);
            if (unlocked)
            {
                Log.Information("Unlocked Achievements");
            }
        }

        /// <summary>
        /// Gets and optionally uses a user's revive.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <param name="removeRevive">Whether to consume a revive.</param>
        /// <returns>True if user has revives available, false otherwise.</returns>
        public async Task<bool> GetUserRevivesAsync(ulong userId, bool removeRevive)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching user info for user {UserId}", userId);

                var userInformation = await GetOrCreateUserInformationAsync(dbContext, userId);

                if (userInformation.Revives > 0)
                {
                    if (removeRevive)
                    {
                        userInformation.Revives--;
                        userInformation.RevivesUsed++;
                        Log.Information(
                            "User {UserId} used a revive. Remaining: {Revives}",
                            userId,
                            userInformation.Revives
                        );

                        await dbContext.SaveChangesAsync();
                    }
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Gets user's preferred language.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <returns>Language code or "en" if not set.</returns>
        public async Task<string> GetUserPreferredLanguageAsync(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                string cacheKey = $"{CacheKeyPrefix}{userId}_Language";

                // Try to get from cache first
                if (
                    _cacheService != null
                    && _cacheService.TryGetValue<string>(cacheKey, out var cachedLanguage)
                    && cachedLanguage != null
                )
                {
                    Log.Debug(
                        "Retrieved preferred language for user {UserId} from cache: {Language}",
                        userId,
                        cachedLanguage
                    );
                    return cachedLanguage;
                }

                // Not in cache, get from database
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Fetching preferred language for user {UserId} from database.",
                    userId
                );

                var userInfo = await dbContext.UserInformation.FindAsync(userId);

                if (userInfo is null)
                {
                    Log.Warning(
                        "No user information found for user {UserId}. Returning default language 'en'.",
                        userId
                    );
                    return "en";
                }

                var language = userInfo.PreferredLanguage ?? "en";

                // Store in cache for future requests
                _cacheService?.Set(cacheKey, language);

                Log.Information(
                    "Preferred language for user {UserId} is '{PreferredLanguage}'.",
                    userId,
                    language
                );
                return language;
            });
        }

        /// <summary>
        /// Sets user's preferred language.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <param name="language">Language code to set.</param>
        public async Task SetPreferredLanguageAsync(ulong userId, string language)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Setting preferred language for user {UserId} to {Language}.",
                    userId,
                    language
                );

                var userInfo = await GetOrCreateUserInformationAsync(dbContext, userId);
                userInfo.PreferredLanguage = language;

                await dbContext.SaveChangesAsync();

                // Update cache
                string userCacheKey = $"{CacheKeyPrefix}{userId}";
                string langCacheKey = $"{CacheKeyPrefix}{userId}_Language";
                _cacheService?.Remove(userCacheKey); // Invalidate full user cache
                _cacheService?.Set(langCacheKey, language); // Update language cache

                Log.Information(
                    "Successfully set preferred language for user {UserId} to '{PreferredLanguage}'.",
                    userId,
                    language
                );
            });
        }

        /// <summary>
        /// Gets a paginated list of user's unlocked achievements.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <param name="pageSize">Number of achievements per page.</param>
        /// <returns>List of achievement definitions.</returns>
        public async Task<List<AchievementDefinition>> GetUnlockedAchievementsAsync(
            ulong userId,
            int pageNumber = 1,
            int pageSize = 10
        )
        {
            using var dbContext = new BotDbContext();
            var userInfo = await dbContext.UserInformation.FindAsync(userId);

            if (userInfo == null)
            {
                return [];
            }

            var unlockedAchievementIds = userInfo
                .UnlockedAchievements.Select(a => a.AchievementId)
                .ToList();
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
                    IsCompleted = unlockedAchievementIds.Contains(a.Id),
                })
                .ToList();

            return paginatedAchievements;
        }

        /// <summary>
        /// Gets or updates the user's current day tracking.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        /// <returns>Current day DateTime.</returns>
        public async Task<DateTime> GetOrUpdateCurrentDay(ulong userId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                var userInfo = await dbContext.UserInformation.FindAsync(userId);
                var currentDay = DateTime.UtcNow;
                var currentWeek = GetStartOfWeek(currentDay);

                if (userInfo is null)
                {
                    Log.Warning(
                        "No user information found for user {UserId}. Cannot set preferred language.",
                        userId
                    );
                    throw new InvalidOperationException(
                        $"No user information found for user {userId}."
                    );
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

                await dbContext.SaveChangesAsync();
                return userInfo.CurrentDay;
            });
        }

        /// <summary>
        /// Gets the start of the week for a given date.
        /// </summary>
        /// <param name="date">Date to get week start for.</param>
        /// <returns>DateTime representing start of week.</returns>
        private static DateTime GetStartOfWeek(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;
            var diff = dayOfWeek - DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;
            return date.AddDays(-diff).Date;
        }

        /// <summary>
        /// Updates the count of incorrect counts for today.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        public async Task UpdateIncorrectCountsToday(ulong userId)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                var userInfo = await dbContext.UserInformation.FindAsync(userId);
                DateTime currentDay = DateTime.UtcNow;

                if (userInfo is null)
                {
                    throw new InvalidOperationException(
                        $"No user information found for user {userId}."
                    );
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

        /// <summary>
        /// Updates the count of correct counts for today.
        /// </summary>
        /// <param name="userInformation">User information object.</param>
        private static async Task UpdateCorrectCountsToday(UserInformation userInformation)
        {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            // Im just gonna supress this as i cant be bothered.
            await ExceptionHandler.HandleAsync(async () =>
            {
                var userInfo = userInformation;
                DateTime currentDay = DateTime.UtcNow;

                if (userInfo is null)
                    throw new InvalidOperationException(
                        $"No user information found for user {userInfo!.UserId}."
                    );

                if (userInfo.CurrentDay.Day == currentDay.Day)
                {
                    userInfo.CorrectCountsToday++;
                }
                else
                {
                    userInfo.CorrectCountsToday = 1;
                }
            });
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        /// <summary>
        /// Deletes all information for a user.
        /// </summary>
        /// <param name="userId">Discord user ID.</param>
        public async Task DeleteUserInformationAsync(ulong userId)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Reseting User {UserId} User Information", userId);
                var userInfo = await dbContext.UserInformation.FindAsync(userId);
                if (userInfo is not null)
                    dbContext.UserInformation.Remove(userInfo);
                await dbContext.SaveChangesAsync();

                // Clear cache for this user
                if (_cacheService != null)
                {
                    _cacheService.RemoveByPattern($"{CacheKeyPrefix}{userId}");
                    Log.Information("Cleared cache for user {UserId}.", userId);
                }
            });
        }

        /// <summary>
        /// Gets or creates user information in the database.
        /// </summary>
        /// <param name="dbContext">Database context.</param>
        /// <param name="userId">Discord user ID.</param>
        /// <returns>User information object.</returns>
        private static async Task<UserInformation> GetOrCreateUserInformationAsync(
            BotDbContext dbContext,
            ulong userId
        )
        {
            var userInformation = await dbContext.UserInformation.FindAsync(userId);

            if (userInformation == null)
            {
                Log.Information(
                    "User information not found for {UserId}, creating new user entry.",
                    userId
                );
                userInformation = new UserInformation
                {
                    UserId = userId,
                    FirstCountTime = DateTime.UtcNow,
                };
                dbContext.UserInformation.Add(userInformation);
            }

            return userInformation;
        }
    }
}
