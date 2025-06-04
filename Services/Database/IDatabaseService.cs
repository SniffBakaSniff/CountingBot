using CountingBot.Database;
using CountingBot.Database.Models;

namespace CountingBot.Services.Database
{
    /// <summary>
    /// Service interface for managing guild (server) settings and permissions in the database.
    /// </summary>
    public interface IGuildSettingsService
    {
        /// <summary>
        /// Gets the command prefix for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <returns>The guild's command prefix</returns>
        Task<string> GetPrefixAsync(ulong guildId);

        /// <summary>
        /// Gets whether math expressions are enabled for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <returns>True if math expressions are enabled, false otherwise</returns>
        Task<bool> GetMathEnabledAsync(ulong guildId);

        /// <summary>
        /// Sets the command prefix for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="prefix">The new command prefix</param>
        Task SetPrefixAsync(ulong guildId, string prefix);

        /// <summary>
        /// Enables or disables math expressions for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="enabled">Whether to enable or disable math expressions</param>
        Task SetMathEnabledAsync(ulong guildId, bool enabled);

        /// <summary>
        /// Configures a channel as a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="channelId">The Discord channel ID</param>
        /// <param name="baseValue">The number base for counting (e.g., 2 for binary, 10 for decimal)</param>
        /// <param name="name">The channel name</param>
        Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name);

        /// <summary>
        /// Checks if a channel is configured for counting.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="channelId">The Discord channel ID</param>
        /// <returns>True if the channel is a counting channel, false otherwise</returns>
        Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId);

        /// <summary>
        /// Gets the number base for a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="channelId">The Discord channel ID</param>
        /// <returns>The number base used for counting</returns>
        Task<int> GetChannelBase(ulong guildId, ulong channelId);

        /// <summary>
        /// Sets the current count for a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="channelId">The Discord channel ID</param>
        /// <param name="currentCount">The new current count</param>
        Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount);

        /// <summary>
        /// Gets the current count for a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="channelId">The Discord channel ID</param>
        /// <returns>The current count in the channel</returns>
        Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId);

        /// <summary>
        /// Gets all counting channels in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <returns>Dictionary of channel names and their IDs</returns>
        Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId);

        /// <summary>
        /// Gets the preferred language for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <returns>The language code (e.g., "en" for English)</returns>
        Task<string> GetGuildPreferredLanguageAsync(ulong guildId);

        /// <summary>
        /// Sets the preferred language for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="language">The language code to set</param>
        Task SetPreferedLanguageAsync(ulong guildId, string language);

        /// <summary>
        /// Deletes all settings for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        Task DeleteGuildSettings(ulong guildId);

        /// <summary>
        /// Gets or creates settings for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="dbContext">The Database Context</param>
        /// <returns>The guild settings object</returns>
        Task<GuildSettings> GetOrCreateGuildSettingsAsync(
            ulong guildId,
            BotDbContext? dbContext = null
        );

        /// <summary>
        /// Sets permissions for a command in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="name">The command name</param>
        /// <param name="enabled">Whether the command is enabled</param>
        /// <param name="user">The user ID to grant permission to</param>
        /// <param name="role">The role ID to grant permission to</param>
        Task SetPermissionsAsync(
            ulong guildId,
            string name,
            bool? enabled,
            ulong? user,
            ulong? role
        );

        /// <summary>
        /// Sets blacklist entries for a command in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="name">The command name</param>
        /// <param name="blacklistedUser">The user ID to blacklist</param>
        /// <param name="blacklistedRole">The role ID to blacklist</param>
        Task SetBlacklistAsync(
            ulong guildId,
            string name,
            ulong? blacklistedUser,
            ulong? blacklistedRole
        );

        /// <summary>
        /// Gets permission data for a command in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="name">The command name</param>
        /// <returns>The command permission data</returns>
        Task<CommandPermissionData?> GetCommandPermissionsAsync(ulong guildId, string name);

        /// <summary>
        /// Removes a permission entry for a command in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="name">The command name</param>
        /// <param name="user">The user ID to remove permission from</param>
        /// <param name="role">The role ID to remove permission from</param>
        Task RemovePermissionEntryAsync(ulong guildId, string name, ulong? user, ulong? role);

        /// <summary>
        /// Removes a blacklist entry for a command in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="name">The command name</param>
        /// <param name="user">The user ID to remove from blacklist</param>
        /// <param name="role">The role ID to remove from blacklist</param>
        Task RemoveBlacklistEntryAsync(ulong guildId, string name, ulong? user, ulong? role);

        /// <summary>
        /// Checks if the current count is the new highscore for a specific channel in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <param name="currentCount">The current count value.</param>
        /// <returns>True if the current count is the new highscore, false otherwise.</returns>
        Task<bool> IsNewHighscore(ulong guildId, ulong channelId, int currentCount);

        /// <summary>
        /// Gets the highscore for a specific channel in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <returns>Returns the highscore for the channel as a string.</returns>
        Task<string> GetChannelHighscore(ulong guildId, ulong channelId);
    }

    /// <summary>
    /// Service interface for managing user information and statistics in the database.
    /// </summary>
    public interface IUserInformationService
    {
        /// <summary>
        /// Gets a user's information and statistics.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <returns>The user information object</returns>
        Task<UserInformation> GetUserInformationAsync(ulong userId);

        /// <summary>
        /// Updates a user's counting statistics.
        /// </summary>
        /// <param name="guildId">The Discord guild ID</param>
        /// <param name="userId">The Discord user ID</param>
        /// <param name="currentCount">The current count value</param>
        /// <param name="correctCount">Whether the count was correct</param>
        Task UpdateUserCountAsync(ulong guildId, ulong userId, int currentCount, bool correctCount);

        /// <summary>
        /// Gets and optionally uses a user's revive token.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <param name="removeRevive">Whether to consume a revive token</param>
        /// <returns>True if the user has available revives, false otherwise</returns>
        Task<bool> GetUserRevivesAsync(ulong userId, bool removeRevive);

        /// <summary>
        /// Gets a user's preferred language.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <returns>The language code (e.g., "en" for English)</returns>
        Task<string> GetUserPreferredLanguageAsync(ulong userId);

        /// <summary>
        /// Sets a user's preferred language.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <param name="language">The language code to set</param>
        Task SetPreferredLanguageAsync(ulong userId, string language);

        /// <summary>
        /// Deletes all information for a user.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        Task DeleteUserInformationAsync(ulong userId);

        /// <summary>
        /// Gets a paginated list of a user's unlocked achievements.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <param name="pageNumber">The page number to retrieve</param>
        /// <param name="pageSize">The number of achievements per page</param>
        /// <returns>List of unlocked achievements</returns>
        Task<List<AchievementDefinition>> GetUnlockedAchievementsAsync(
            ulong userId,
            int pageNumber = 1,
            int pageSize = 10
        );

        /// <summary>
        /// Gets or updates the current day for a user's statistics.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        /// <returns>The current day timestamp</returns>
        Task<DateTime> GetOrUpdateCurrentDay(ulong userId);

        /// <summary>
        /// Updates the count of incorrect counts for a user today.
        /// </summary>
        /// <param name="userId">The Discord user ID</param>
        Task UpdateIncorrectCountsToday(ulong userId);
    }

    /// <summary>
    /// Service interface for managing leaderboards.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// Gets paginated leaderboard data for a guild.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve</param>
        /// <param name="pageSize">The number of entries per page</param>
        /// <param name="guildId">The Discord guild ID</param>
        /// <returns>Dictionary of leaderboard categories and their user lists</returns>
        Task<Dictionary<string, List<UserInformation>>> GetLeaderboardsAsync(
            int pageNumber,
            int pageSize,
            ulong guildId
        );
    }
}
