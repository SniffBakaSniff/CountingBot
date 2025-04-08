using Microsoft.EntityFrameworkCore;
using Serilog;

using CountingBot.Database;
using CountingBot.Database.Models;
using System.Runtime.CompilerServices;


namespace CountingBot.Services.Database
{
    /// <summary>
    /// Provides methods for interacting with the GuildSettings table in the database.
    /// </summary>
    public class GuildSettingsService : IGuildSettingsService
    {
        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching prefix for guild {GuildId}", guildId);

                var settings = await dbContext.GuildSettings.FindAsync(guildId);
                var prefix = settings?.Prefix ?? "!";
                Log.Information("Prefix for guild {GuildId} is {Prefix}", guildId, prefix);
                return prefix;
            });
        }

        public async Task<bool> GetMathEnabledAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching prefix for guild {GuildId}", guildId);

                var settings = await dbContext.GuildSettings.FindAsync(guildId);
                var setting = settings!.MathEnabled;
                Log.Information("Is Math Enabled for Guild {GuildId} : {Setting}", guildId, setting);
                return setting;
            });
        }

        public async Task SetPrefixAsync(ulong guildId, string prefix)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting prefix for guild {GuildId} to {Prefix}", guildId, prefix);

                var settings = await GetOrCreateGuildSettingsAsync(guildId);
                settings.Prefix = prefix;

                await dbContext.SaveChangesAsync();
                Log.Information("Prefix updated successfully for guild {GuildId}", guildId);
            });
        }

        public async Task SetMathEnabledAsync(ulong guildId, bool enabled)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting MathEnabled to {Enabled} for guild {GuildId}", enabled, guildId);

                var settings = await GetOrCreateGuildSettingsAsync(guildId);
                settings.MathEnabled = enabled;

                await dbContext.SaveChangesAsync();
                Log.Information("MathEnabled successfully updated to {Enabled} for guild {GuildId}", enabled, guildId);
            });
        }

        public async Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting counting channel {ChannelId} for guild {GuildId}", channelId, guildId);

                var channelSettings = await GetOrCreateChannelSettingsAsync(dbContext, guildId, channelId);
                channelSettings.Base = baseValue;
                channelSettings.Name = name;

                await dbContext.SaveChangesAsync();
                Log.Information("Counting channel {ChannelId} updated for guild {GuildId}", channelId, guildId);
            });
        }

        public async Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Updating current count for channel {ChannelId} in guild {GuildId} to {CurrentCount}", 
                    channelId, guildId, currentCount);

                var channelSettings = await GetOrCreateChannelSettingsAsync(dbContext, guildId, channelId);
                channelSettings.CurrentCount = currentCount;
                if (currentCount > channelSettings.Highescore)
                {
                    channelSettings.Highescore++;
                }

                await dbContext.SaveChangesAsync();
                Log.Information("Current count updated for channel {ChannelId} in guild {GuildId}", channelId, guildId);
            });
        }

        public async Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId)
        {
            Log.Information("Checking if channel {ChannelId} is a counting channel in guild {GuildId}", channelId, guildId);

            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    ;

                bool isCountingChannel = channelSettings != null;
                Log.Information("Channel {ChannelId} in guild {GuildId} is{Status} a counting channel.",
                    channelId, guildId, isCountingChannel ? "" : " not");
                return isCountingChannel;
            });
        }

        public async Task<int> GetChannelBase(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching base value for channel {ChannelId} in guild {GuildId}", channelId, guildId);

                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    ;

                int baseValue = channelSettings?.Base ?? 10;
                Log.Information("Base value for channel {ChannelId} is {BaseValue}", channelId, baseValue);
                return baseValue;
            });
        }

        public async Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching current count for channel {ChannelId} in guild {GuildId}", channelId, guildId);

                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    ;

                if (channelSettings is null)
                {
                    Log.Warning("No settings found for channel {ChannelId} in guild {GuildId}. Returning 0.", channelId, guildId);
                    return 0;
                }

                Log.Information("Current count for channel {ChannelId} is {CurrentCount}", channelId, channelSettings.CurrentCount);
                return channelSettings.CurrentCount;
            });
        }

        public async Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching counting channels for guild {GuildId}", guildId);

                var channels = await dbContext.ChannelSettings
                    .Where(c => c.GuildId == guildId)
                    .Select(c => new { Name = c.Name ?? c.ChannelId.ToString(), c.ChannelId })
                    .ToDictionaryAsync(c => c.Name, c => c.ChannelId)
                    ;

                var allChannels = await dbContext.ChannelSettings
                    .Where(c => c.GuildId == guildId)
                    .ToListAsync()
                    ;

                foreach (var channel in allChannels)
                {
                    Log.Debug("Channel: {ChannelId}, Guild: {GuildId}, Name: {Name}, Base: {Base}, CurrentCount: {CurrentCount}", 
                        channel.ChannelId, channel.GuildId, channel.Name, channel.Base, channel.CurrentCount);
                }

                Log.Information("Found {ChannelCount} counting channels for guild {GuildId}", channels.Count, guildId);
                return channels;
            });
        }

        public async Task<string> GetGuildPreferredLanguageAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching preferred language for guild {GuildId}.", guildId);

                var guildSettings = await dbContext.GuildSettings.FindAsync(guildId);

                if (guildSettings is null)
                {
                    Log.Warning("No settings found for guild {GuildId}. Returning default language 'en'.", guildId);
                    return "en";
                }

                Log.Information("Preferred language for guild {GuildId} is '{PreferredLanguage}'.", guildId, guildSettings.PreferredLanguage);
                return guildSettings.PreferredLanguage ?? "en";
            });
        }

        public async Task SetPreferedLanguageAsync(ulong guildId, string language)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting Prefered Language for guild {GuildId} to {Language}", guildId, language);

                var settings = await GetOrCreateGuildSettingsAsync(guildId);
                settings.PreferredLanguage = language;

                await dbContext.SaveChangesAsync();
                Log.Information("Prefered Language updated successfully for guild {GuildId}", guildId);
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
        public async Task SetPermissionsAsync(ulong guildId, string name, bool? enabled, ulong? user, ulong? role)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();

                Log.Information("Starting SetPermissionsAsync for guild {GuildId}, command {CommandName}", guildId, name);

                var settings = await GetOrCreateGuildSettingsAsync(guildId);
                Log.Debug("Fetched settings for guild {GuildId}: Prefix={Prefix}, Language={Language}",
                    guildId, settings.Prefix, settings.PreferredLanguage);

                if (!settings.CommandPermissions.ContainsKey(name))
                {
                    Log.Debug("No existing permissions for command {CommandName}. Creating new entry.", name);
                    settings.CommandPermissions[name] = new CommandPermissionData();
                }

                var commandPermissions = settings.CommandPermissions[name];

                commandPermissions.Enabled = enabled ?? true;
                Log.Debug("Set 'Enabled' to {Enabled} for command {CommandName}", commandPermissions.Enabled, name);

                commandPermissions.Users = user.HasValue ? new List<ulong> { user.Value } : new List<ulong>();
                commandPermissions.Roles = role.HasValue ? new List<ulong> { role.Value } : new List<ulong>();

                Log.Debug("Assigned Users: [{Users}] | Assigned Roles: [{Roles}] for command {CommandName}", 
                    string.Join(", ", commandPermissions.Users), 
                    string.Join(", ", commandPermissions.Roles), 
                    name);

                dbContext.Entry(settings).State = EntityState.Modified;
                await dbContext.SaveChangesAsync();

                Log.Information("Permissions successfully updated for command {CommandName} in guild {GuildId}", name, guildId);
            });
        }




        public async Task DeleteGuildSettings(ulong guildId)
        {
            using var dbContext = new BotDbContext();

            var guildSettings = await dbContext.GuildSettings.FindAsync(guildId);
            var channelSettings = await dbContext.ChannelSettings
                .Where(c => c.GuildId == guildId)
                .ToListAsync()
                ;

            if (guildSettings is null && channelSettings.Count == 0)
            {
                Log.Information("No GuildSettings or ChannelSettings found for guild {GuildId}.", guildId);
                return;
            }

            if (guildSettings != null)
            {
                dbContext.GuildSettings.Remove(guildSettings);
                Log.Information("Removed GuildSettings for guild {GuildId}.", guildId);
            }
            else
            {
                Log.Information("No GuildSettings found for guild {GuildId}.", guildId);
            }

            if (channelSettings.Count > 0)
            {
                dbContext.ChannelSettings.RemoveRange(channelSettings);
                Log.Information("Removed {Count} ChannelSettings for guild {GuildId}.", channelSettings.Count, guildId);
            }
            else
            {
                Log.Information("No ChannelSettings found for guild {GuildId}.", guildId);
            }

            await dbContext.SaveChangesAsync();
            Log.Information("Completed deletion of settings for guild {GuildId}.", guildId);
        }

        public async Task<GuildSettings> GetOrCreateGuildSettingsAsync(ulong guildId)
        {
            using var dbContext = new BotDbContext();
            var settings = await dbContext.GuildSettings.FindAsync(guildId);
            if (settings is null)
            {
                Log.Information("No GuildSettings found for guild {GuildId}. Creating new settings entry.", guildId);

                settings = new GuildSettings 
                { 
                    GuildId = guildId,
                    Prefix = "!",
                    MathEnabled = false,
                    PreferredLanguage = "en"
                };

                dbContext.GuildSettings.Add(settings);
                await dbContext.SaveChangesAsync();
                Log.Information("Created new GuildSettings for guild {GuildId}: {@GuildSettings}", guildId, settings);
            }
            return settings;
        }

        private async Task<ChannelSettings> GetOrCreateChannelSettingsAsync(BotDbContext dbContext, ulong guildId, ulong channelId)
        {
            var channelSettings = await dbContext.ChannelSettings
                .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                ;

            if (channelSettings is null)
            {
                Log.Information("No ChannelSettings found for channel {ChannelId} in guild {GuildId}. Creating new entry.", channelId, guildId);
                channelSettings = new ChannelSettings
                {
                    GuildId = guildId,
                    ChannelId = channelId
                };
                dbContext.ChannelSettings.Add(channelSettings);
                await dbContext.SaveChangesAsync();
            }
            return channelSettings;
        }
    }
}