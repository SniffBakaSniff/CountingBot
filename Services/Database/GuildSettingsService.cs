using CountingBot.Database;
using CountingBot.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CountingBot.Services.Database
{
    /// <summary>
    /// Provides methods for managing guild-specific settings and configurations in the database.
    /// Implements IGuildSettingsService interface with caching support.
    /// </summary>
    public class GuildSettingsService : IGuildSettingsService
    {
        private readonly ICacheService? _cacheService;
        private const string CacheKeyPrefix = "GuildSettings_";

        /// <summary>
        /// Initializes a new instance of the GuildSettingsService.
        /// </summary>
        public GuildSettingsService()
        {
            _cacheService = null;
        }

        /// <summary>
        /// Initializes a new instance of the GuildSettingsService with caching support.
        /// </summary>
        /// <param name="cacheService">The cache service to use.</param>
        public GuildSettingsService(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        /// <summary>
        /// Retrieves the command prefix for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <returns>The guild's custom prefix or the default "!" if not set.</returns>
        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                string cacheKey = $"{CacheKeyPrefix}{guildId}_Prefix";

                // Try to get from cache first
                if (
                    _cacheService != null
                    && _cacheService.TryGetValue<string>(cacheKey, out var cachedPrefix)
                    && cachedPrefix != null
                )
                {
                    Log.Debug(
                        "Retrieved prefix for guild {GuildId} from cache: {Prefix}",
                        guildId,
                        cachedPrefix
                    );
                    return cachedPrefix;
                }

                // Not in cache, get from database
                using var dbContext = new BotDbContext();
                Log.Information("Fetching prefix for guild {GuildId} from database", guildId);

                var settings = await dbContext.GuildSettings.FindAsync(guildId);
                var prefix = settings?.Prefix ?? "!";

                // Store in cache for future requests
                _cacheService?.Set(cacheKey, prefix);

                Log.Information("Prefix for guild {GuildId} is {Prefix}", guildId, prefix);
                return prefix;
            });
        }

        /// <summary>
        /// Checks if math operations are enabled for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <returns>True if math operations are enabled, false otherwise.</returns>
        public async Task<bool> GetMathEnabledAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                string cacheKey = $"{CacheKeyPrefix}{guildId}_MathEnabled";

                // Try to get from cache first
                if (
                    _cacheService != null
                    && _cacheService.TryGetValue<bool>(cacheKey, out var cachedSetting)
                )
                {
                    Log.Debug(
                        "Retrieved math enabled setting for guild {GuildId} from cache: {Setting}",
                        guildId,
                        cachedSetting
                    );
                    return cachedSetting;
                }

                // Not in cache, get from database
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Fetching math enabled setting for guild {GuildId} from database",
                    guildId
                );

                var settings = await dbContext.GuildSettings.FindAsync(guildId);
                if (settings == null)
                {
                    return false; // Default value if no settings exist
                }

                var setting = settings.MathEnabled;

                // Store in cache for future requests
                _cacheService?.Set(cacheKey, setting);

                Log.Information(
                    "Is Math Enabled for Guild {GuildId} : {Setting}",
                    guildId,
                    setting
                );
                return setting;
            });
        }

        /// <summary>
        /// Sets the command prefix for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="prefix">The new prefix to set.</param>
        public async Task SetPrefixAsync(ulong guildId, string prefix)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting prefix for guild {GuildId} to {Prefix}", guildId, prefix);

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                settings.Prefix = prefix;

                await dbContext.SaveChangesAsync();

                // Update cache
                string cacheKey = $"{CacheKeyPrefix}{guildId}_Prefix";
                _cacheService?.Set(cacheKey, prefix);

                Log.Information("Prefix updated successfully for guild {GuildId}", guildId);
            });
        }

        /// <summary>
        /// Enables or disables math operations for a specific guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="enabled">True to enable math operations, false to disable.</param>
        public async Task SetMathEnabledAsync(ulong guildId, bool enabled)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Setting MathEnabled to {Enabled} for guild {GuildId}",
                    enabled,
                    guildId
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                settings.MathEnabled = enabled;

                await dbContext.SaveChangesAsync();

                // Update cache
                string cacheKey = $"{CacheKeyPrefix}{guildId}_MathEnabled";
                _cacheService?.Set(cacheKey, enabled);

                Log.Information(
                    "MathEnabled successfully updated to {Enabled} for guild {GuildId}",
                    enabled,
                    guildId
                );
            });
        }

        /// <summary>
        /// Configures a channel as a counting channel with specific settings.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <param name="baseValue">The base number system to use (e.g., 10 for decimal).</param>
        /// <param name="name">The display name for the counting channel.</param>
        public async Task SetCountingChannel(
            ulong guildId,
            ulong channelId,
            int baseValue,
            string name
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Setting counting channel {ChannelId} for guild {GuildId}",
                    channelId,
                    guildId
                );

                var channelSettings = await GetOrCreateChannelSettingsAsync(
                    dbContext,
                    guildId,
                    channelId
                );
                channelSettings.Base = baseValue;
                channelSettings.Name = name;

                await dbContext.SaveChangesAsync();
                Log.Information(
                    "Counting channel {ChannelId} updated for guild {GuildId}",
                    channelId,
                    guildId
                );
            });
        }

        /// <summary>
        /// Updates the current count for a specific counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <param name="currentCount">The new current count value.</param>
        public async Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Updating current count for channel {ChannelId} in guild {GuildId} to {CurrentCount}",
                    channelId,
                    guildId,
                    currentCount
                );

                var channelSettings = await GetOrCreateChannelSettingsAsync(
                    dbContext,
                    guildId,
                    channelId
                );
                channelSettings.CurrentCount = currentCount;
                if (currentCount > channelSettings.Highescore)
                {
                    channelSettings.Highescore++;
                }

                await dbContext.SaveChangesAsync();
                Log.Information(
                    "Current count updated for channel {ChannelId} in guild {GuildId}",
                    channelId,
                    guildId
                );
            });
        }

        /// <summary>
        /// Checks if a specific channel is configured as a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID to check.</param>
        /// <returns>True if the channel is a counting channel, false otherwise.</returns>
        public async Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId)
        {
            Log.Information(
                "Checking if channel {ChannelId} is a counting channel in guild {GuildId}",
                channelId,
                guildId
            );

            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                var channelSettings = await dbContext.ChannelSettings.SingleOrDefaultAsync(c =>
                    c.ChannelId == channelId && c.GuildId == guildId
                );

                bool isCountingChannel = channelSettings != null;
                Log.Information(
                    "Channel {ChannelId} in guild {GuildId} is{Status} a counting channel.",
                    channelId,
                    guildId,
                    isCountingChannel ? "" : " not"
                );
                return isCountingChannel;
            });
        }

        /// <summary>
        /// Gets the base number system used in a counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <returns>The base value (defaults to 10 if not set).</returns>
        public async Task<int> GetChannelBase(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Fetching base value for channel {ChannelId} in guild {GuildId}",
                    channelId,
                    guildId
                );

                var channelSettings = await dbContext.ChannelSettings.SingleOrDefaultAsync(c =>
                    c.ChannelId == channelId && c.GuildId == guildId
                );

                int baseValue = channelSettings?.Base ?? 10;
                Log.Information(
                    "Base value for channel {ChannelId} is {BaseValue}",
                    channelId,
                    baseValue
                );
                return baseValue;
            });
        }

        /// <summary>
        /// Gets the current count for a specific counting channel.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <returns>The current count value (0 if not set).</returns>
        public async Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Fetching current count for channel {ChannelId} in guild {GuildId}",
                    channelId,
                    guildId
                );

                var channelSettings = await dbContext.ChannelSettings.SingleOrDefaultAsync(c =>
                    c.ChannelId == channelId && c.GuildId == guildId
                );

                if (channelSettings is null)
                {
                    Log.Warning(
                        "No settings found for channel {ChannelId} in guild {GuildId}. Returning 0.",
                        channelId,
                        guildId
                    );
                    return 0;
                }

                Log.Information(
                    "Current count for channel {ChannelId} is {CurrentCount}",
                    channelId,
                    channelSettings.CurrentCount
                );
                return channelSettings.CurrentCount;
            });
        }

        /// <summary>
        /// Gets all counting channels in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <returns>A dictionary mapping channel names to channel IDs.</returns>
        public async Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching counting channels for guild {GuildId}", guildId);

                var channels = await dbContext
                    .ChannelSettings.Where(c => c.GuildId == guildId)
                    .Select(c => new { Name = c.Name ?? c.ChannelId.ToString(), c.ChannelId })
                    .ToDictionaryAsync(c => c.Name, c => c.ChannelId);

                return channels;
            });
        }

        /// <summary>
        /// Gets the preferred language for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <returns>The preferred language code (defaults to "en" if not set).</returns>
        public async Task<string> GetGuildPreferredLanguageAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                string cacheKey = $"{CacheKeyPrefix}{guildId}_Language";

                // Try to get from cache first
                if (
                    _cacheService != null
                    && _cacheService.TryGetValue<string>(cacheKey, out var cachedLanguage)
                    && cachedLanguage != null
                )
                {
                    Log.Debug(
                        "Retrieved preferred language for guild {GuildId} from cache: {Language}",
                        guildId,
                        cachedLanguage
                    );
                    return cachedLanguage;
                }

                // Not in cache, get from database
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Fetching preferred language for guild {GuildId} from database.",
                    guildId
                );

                var guildSettings = await dbContext.GuildSettings.FindAsync(guildId);

                if (guildSettings is null)
                {
                    Log.Warning(
                        "No settings found for guild {GuildId}. Returning default language 'en'.",
                        guildId
                    );
                    return "en";
                }

                var language = guildSettings.PreferredLanguage ?? "en";

                // Store in cache for future requests
                _cacheService?.Set(cacheKey, language);

                Log.Information(
                    "Preferred language for guild {GuildId} is '{PreferredLanguage}'.",
                    guildId,
                    language
                );
                return language;
            });
        }

        /// <summary>
        /// Sets the preferred language for a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="language">The language code to set.</param>
        public async Task SetPreferedLanguageAsync(ulong guildId, string language)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information(
                    "Setting Prefered Language for guild {GuildId} to {Language}",
                    guildId,
                    language
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                settings.PreferredLanguage = language;

                await dbContext.SaveChangesAsync();

                // Update cache
                string cacheKey = $"{CacheKeyPrefix}{guildId}_Language";
                _cacheService?.Set(cacheKey, language);

                Log.Information(
                    "Prefered Language updated successfully for guild {GuildId}",
                    guildId
                );
            });
        }

        /// <summary>
        /// Updates or sets the permission configuration for a specific command in a guild.
        /// This includes whether the command is enabled and which users or roles are allowed to use it.
        /// If the command does not already have a configuration entry, one will be created.
        /// </summary>
        /// <param name="guildId">The ID of the guild the permissions apply to.</param>
        /// <param name="name">The PermissionKey of the command to configure permissions for.</param>
        /// <param name="enabled">Whether the command is enabled (true), disabled (false), or unchanged (null).</param>
        /// <param name="users">A user ID allowed to use the command. Pass null to leave unchanged.</param>
        /// <param name="roles">A role ID allowed to use the command. Pass null to leave unchanged.</param>
        public async Task SetPermissionsAsync(
            ulong guildId,
            string name,
            bool? enabled,
            ulong? user,
            ulong? role
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information(
                    "Starting SetPermissionsAsync for guild {GuildId}, command {CommandName}",
                    guildId,
                    name
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                Log.Debug(
                    "Fetched settings for guild {GuildId}: Prefix={Prefix}, Language={Language}",
                    guildId,
                    settings.Prefix,
                    settings.PreferredLanguage
                );

                if (!settings.CommandPermissions.ContainsKey(name))
                {
                    Log.Debug(
                        "No existing permissions for command {CommandName}. Creating new entry.",
                        name
                    );
                    settings.CommandPermissions[name] = new CommandPermissionData();
                }

                var commandPermissions = settings.CommandPermissions[name];

                commandPermissions.Enabled = enabled ?? true;
                Log.Debug(
                    "Set 'Enabled' to {Enabled} for command {CommandName}",
                    commandPermissions.Enabled,
                    name
                );

                commandPermissions.Users = user.HasValue
                    ? new List<ulong> { user.Value }
                    : new List<ulong>();
                commandPermissions.Roles = role.HasValue
                    ? new List<ulong> { role.Value }
                    : new List<ulong>();

                Log.Debug(
                    "Assigned Users: [{Users}] | Assigned Roles: [{Roles}] for command {CommandName}",
                    string.Join(", ", commandPermissions.Users),
                    string.Join(", ", commandPermissions.Roles),
                    name
                );

                dbContext.Entry(settings).State = EntityState.Modified;
                await dbContext.SaveChangesAsync();

                Log.Information(
                    "Permissions successfully updated for command {CommandName} in guild {GuildId}",
                    name,
                    guildId
                );
            });
        }

        /// <summary>
        /// Updates the blacklist configuration for a specific command in a guild.
        /// This allows blacklisting specific users or roles from using a command.
        /// If the command does not already have a configuration entry, one will be created.
        /// </summary>
        /// <param name="guildId">The ID of the guild the blacklist applies to.</param>
        /// <param name="name">The PermissionKey of the command to configure blacklist for.</param>
        /// <param name="blacklistedUser">A user ID to blacklist from using the command. Pass null to leave unchanged.</param>
        /// <param name="blacklistedRole">A role ID to blacklist from using the command. Pass null to leave unchanged.</param>
        public async Task SetBlacklistAsync(
            ulong guildId,
            string name,
            ulong? blacklistedUser,
            ulong? blacklistedRole
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information(
                    "Starting SetBlacklistAsync for guild {GuildId}, command {CommandName}",
                    guildId,
                    name
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                Log.Debug(
                    "Fetched settings for guild {GuildId}: Prefix={Prefix}, Language={Language}",
                    guildId,
                    settings.Prefix,
                    settings.PreferredLanguage
                );

                if (!settings.CommandPermissions.ContainsKey(name))
                {
                    Log.Debug(
                        "No existing permissions for command {CommandName}. Creating new entry.",
                        name
                    );
                    settings.CommandPermissions[name] = new CommandPermissionData();
                }

                var commandPermissions = settings.CommandPermissions[name];

                // Add to blacklisted users if specified
                if (
                    blacklistedUser.HasValue
                    && !commandPermissions.BlacklistedUsers.Contains(blacklistedUser.Value)
                )
                {
                    commandPermissions.BlacklistedUsers.Add(blacklistedUser.Value);
                    Log.Debug(
                        "Added user {UserId} to blacklist for command {CommandName}",
                        blacklistedUser.Value,
                        name
                    );
                }

                // Add to blacklisted roles if specified
                if (
                    blacklistedRole.HasValue
                    && !commandPermissions.BlacklistedRoles.Contains(blacklistedRole.Value)
                )
                {
                    commandPermissions.BlacklistedRoles.Add(blacklistedRole.Value);
                    Log.Debug(
                        "Added role {RoleId} to blacklist for command {CommandName}",
                        blacklistedRole.Value,
                        name
                    );
                }

                Log.Debug(
                    "Blacklisted Users: [{Users}] | Blacklisted Roles: [{Roles}] for command {CommandName}",
                    string.Join(", ", commandPermissions.BlacklistedUsers),
                    string.Join(", ", commandPermissions.BlacklistedRoles),
                    name
                );

                dbContext.Entry(settings).State = EntityState.Modified;
                await dbContext.SaveChangesAsync();

                Log.Information(
                    "Blacklist successfully updated for command {CommandName} in guild {GuildId}",
                    name,
                    guildId
                );
            });
        }

        /// <summary>
        /// Retrieves the permission data for a specific command in a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to get permissions from.</param>
        /// <param name="name">The PermissionKey of the command to get permissions for.</param>
        /// <returns>The CommandPermissionData for the specified command, or null if not found.</returns>
        public async Task<CommandPermissionData?> GetCommandPermissionsAsync(
            ulong guildId,
            string name
        )
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information(
                    "Getting permissions for command {CommandName} in guild {GuildId}",
                    name,
                    guildId
                );

                var settings = await dbContext.GuildSettings.FindAsync(guildId);
                if (settings == null)
                {
                    Log.Debug("No settings found for guild {GuildId}", guildId);
                    return null;
                }

                if (!settings.CommandPermissions.TryGetValue(name, out var permissionData))
                {
                    Log.Debug(
                        "No permission data found for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                    return null;
                }

                Log.Debug(
                    "Retrieved permission data for command {CommandName} in guild {GuildId}",
                    name,
                    guildId
                );
                return permissionData;
            });
        }

        /// <summary>
        /// Removes a user or role from the allowed list for a specific command in a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to modify permissions for.</param>
        /// <param name="name">The PermissionKey of the command to modify permissions for.</param>
        /// <param name="user">The user ID to remove from the allowed list. Pass null to skip.</param>
        /// <param name="role">The role ID to remove from the allowed list. Pass null to skip.</param>
        public async Task RemovePermissionEntryAsync(
            ulong guildId,
            string name,
            ulong? user,
            ulong? role
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information(
                    "Starting RemovePermissionEntryAsync for guild {GuildId}, command {CommandName}",
                    guildId,
                    name
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                if (!settings.CommandPermissions.TryGetValue(name, out var permissionData))
                {
                    Log.Debug(
                        "No permission data found for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                    return;
                }

                bool modified = false;

                // Remove user if specified
                if (user.HasValue && permissionData.Users.Contains(user.Value))
                {
                    permissionData.Users.Remove(user.Value);
                    Log.Debug(
                        "Removed user {UserId} from allowed list for command {CommandName}",
                        user.Value,
                        name
                    );
                    modified = true;
                }

                // Remove role if specified
                if (role.HasValue && permissionData.Roles.Contains(role.Value))
                {
                    permissionData.Roles.Remove(role.Value);
                    Log.Debug(
                        "Removed role {RoleId} from allowed list for command {CommandName}",
                        role.Value,
                        name
                    );
                    modified = true;
                }

                if (modified)
                {
                    dbContext.Entry(settings).State = EntityState.Modified;
                    await dbContext.SaveChangesAsync();
                    Log.Information(
                        "Permission entry successfully removed for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                }
                else
                {
                    Log.Information(
                        "No changes made to permissions for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                }
            });
        }

        /// <summary>
        /// Removes a user or role from the blacklist for a specific command in a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to modify blacklist for.</param>
        /// <param name="name">The PermissionKey of the command to modify blacklist for.</param>
        /// <param name="user">The user ID to remove from the blacklist. Pass null to skip.</param>
        /// <param name="role">The role ID to remove from the blacklist. Pass null to skip.</param>
        public async Task RemoveBlacklistEntryAsync(
            ulong guildId,
            string name,
            ulong? user,
            ulong? role
        )
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information(
                    "Starting RemoveBlacklistEntryAsync for guild {GuildId}, command {CommandName}",
                    guildId,
                    name
                );

                var settings = await GetOrCreateGuildSettingsAsync(guildId, dbContext);
                if (!settings.CommandPermissions.TryGetValue(name, out var permissionData))
                {
                    Log.Debug(
                        "No permission data found for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                    return;
                }

                bool modified = false;

                // Remove user if specified
                if (user.HasValue && permissionData.BlacklistedUsers.Contains(user.Value))
                {
                    permissionData.BlacklistedUsers.Remove(user.Value);
                    Log.Debug(
                        "Removed user {UserId} from blacklist for command {CommandName}",
                        user.Value,
                        name
                    );
                    modified = true;
                }

                // Remove role if specified
                if (role.HasValue && permissionData.BlacklistedRoles.Contains(role.Value))
                {
                    permissionData.BlacklistedRoles.Remove(role.Value);
                    Log.Debug(
                        "Removed role {RoleId} from blacklist for command {CommandName}",
                        role.Value,
                        name
                    );
                    modified = true;
                }

                if (modified)
                {
                    dbContext.Entry(settings).State = EntityState.Modified;
                    await dbContext.SaveChangesAsync();
                    Log.Information(
                        "Blacklist entry successfully removed for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                }
                else
                {
                    Log.Information(
                        "No changes made to blacklist for command {CommandName} in guild {GuildId}",
                        name,
                        guildId
                    );
                }
            });
        }

        /// <summary>
        /// Checks if the current count is the new highscore for a specific channel in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <param name="currentCount">The current count value.</param>
        /// <returns>True if the current count is the new highscore, false otherwise.</returns>
        public async Task<bool> IsNewHighscore(ulong guildId, ulong channelId, int currentCount)
        {
            using var dbContext = new BotDbContext();

            int channelHighscore = await dbContext
                .ChannelSettings.Where(c => c.ChannelId == channelId && c.GuildId == guildId)
                .Select(c => c.Highescore)
                .FirstOrDefaultAsync();
            return currentCount > channelHighscore;
        }

        /// <summary>
        /// Gets the highscore for a specific channel in a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <returns>Returns the highscore for the channel as an integer.</returns>
        public async Task<string> GetChannelHighscore(ulong guildId, ulong channelId)
        {
            using var dbContext = new BotDbContext();

            int channelHighscore = await dbContext
                .ChannelSettings.Where(c => c.ChannelId == channelId && c.GuildId == guildId)
                .Select(c => c.Highescore)
                .FirstOrDefaultAsync();

            int channelBase = await dbContext
                .ChannelSettings.Where(c => c.ChannelId == channelId && c.GuildId == guildId)
                .Select(c => c.Base)
                .FirstOrDefaultAsync();

            string formattedHighscore = Convert.ToString(channelHighscore, channelBase);

            return formattedHighscore;
        }

        /// <summary>
        /// Deletes all settings associated with a guild.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        public async Task DeleteGuildSettings(ulong guildId)
        {
            using var dbContext = new BotDbContext();

            var guildSettings = await dbContext.GuildSettings.FindAsync(guildId);
            var channelSettings = await dbContext
                .ChannelSettings.Where(c => c.GuildId == guildId)
                .ToListAsync();

            if (guildSettings is null && channelSettings.Count == 0)
            {
                Log.Information(
                    "No GuildSettings or ChannelSettings found for guild {GuildId}.",
                    guildId
                );
                return;
            }

            if (guildSettings != null)
            {
                dbContext.GuildSettings.Remove(guildSettings);
                Log.Information("Removed GuildSettings for guild {GuildId}.", guildId);

                // Clear cache for this guild
                if (_cacheService != null)
                {
                    _cacheService.RemoveByPattern($"{CacheKeyPrefix}{guildId}");
                    Log.Information("Cleared cache for guild {GuildId}.", guildId);
                }
            }
            else
            {
                Log.Information("No GuildSettings found for guild {GuildId}.", guildId);
            }

            if (channelSettings.Count > 0)
            {
                dbContext.ChannelSettings.RemoveRange(channelSettings);
                Log.Information(
                    "Removed {Count} ChannelSettings for guild {GuildId}.",
                    channelSettings.Count,
                    guildId
                );
            }
            else
            {
                Log.Information("No ChannelSettings found for guild {GuildId}.", guildId);
            }

            await dbContext.SaveChangesAsync();
            Log.Information("Completed deletion of settings for guild {GuildId}.", guildId);
        }

        /// <summary>
        /// Gets existing guild settings or creates new ones if they don't exist.
        /// </summary>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="dbContext">The Database Context</param>
        /// <returns>The guild settings object.</returns>
        public async Task<GuildSettings> GetOrCreateGuildSettingsAsync(
            ulong guildId,
            BotDbContext? dbContext = null
        )
        {
            dbContext ??= new BotDbContext();
            var settings = await dbContext.GuildSettings.FindAsync(guildId);
            if (settings is null)
            {
                Log.Information(
                    "No GuildSettings found for guild {GuildId}. Creating new settings entry.",
                    guildId
                );

                settings = new GuildSettings
                {
                    GuildId = guildId,
                    Prefix = "!",
                    MathEnabled = false,
                    PreferredLanguage = "en",
                };

                dbContext.GuildSettings.Add(settings);
                await dbContext.SaveChangesAsync();
                Log.Information(
                    "Created new GuildSettings for guild {GuildId}: {@GuildSettings}",
                    guildId,
                    settings
                );
            }
            return settings;
        }

        /// <summary>
        /// Gets existing channel settings or creates new ones if they don't exist.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <returns>The channel settings object.</returns>
        private async Task<ChannelSettings> GetOrCreateChannelSettingsAsync(
            BotDbContext dbContext,
            ulong guildId,
            ulong channelId
        )
        {
            var channelSettings = await dbContext.ChannelSettings.SingleOrDefaultAsync(c =>
                c.ChannelId == channelId && c.GuildId == guildId
            );

            if (channelSettings is null)
            {
                Log.Information(
                    "No ChannelSettings found for channel {ChannelId} in guild {GuildId}. Creating new entry.",
                    channelId,
                    guildId
                );
                channelSettings = new ChannelSettings { GuildId = guildId, ChannelId = channelId };
                dbContext.ChannelSettings.Add(channelSettings);
                await dbContext.SaveChangesAsync();
            }
            return channelSettings;
        }
    }
}
