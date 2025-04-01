using Microsoft.EntityFrameworkCore;
using Serilog;

using CountingBot.Database;

namespace CountingBot.Services.Database
{
    public class GuildSettingsService : IGuildSettingsService
    {
        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching prefix for guild {GuildId}", guildId);

                var settings = await dbContext.GuildSettings.FindAsync(guildId).ConfigureAwait(false);
                var prefix = settings?.Prefix ?? "!";
                Log.Information("Prefix for guild {GuildId} is {Prefix}", guildId, prefix);
                return prefix;
            }).ConfigureAwait(false);
        }

        public async Task SetPrefixAsync(ulong guildId, string prefix)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting prefix for guild {GuildId} to {Prefix}", guildId, prefix);

                var settings = await GetOrCreateGuildSettingsAsync(dbContext, guildId).ConfigureAwait(false);
                settings.Prefix = prefix;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Prefix updated successfully for guild {GuildId}", guildId);
            }).ConfigureAwait(false);
        }

        public async Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Setting counting channel {ChannelId} for guild {GuildId}", channelId, guildId);

                var channelSettings = await GetOrCreateChannelSettingsAsync(dbContext, guildId, channelId).ConfigureAwait(false);
                channelSettings.Base = baseValue;
                channelSettings.Name = name;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Counting channel {ChannelId} updated for guild {GuildId}", channelId, guildId);
            }).ConfigureAwait(false);
        }

        public async Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Updating current count for channel {ChannelId} in guild {GuildId} to {CurrentCount}", 
                    channelId, guildId, currentCount);

                var channelSettings = await GetOrCreateChannelSettingsAsync(dbContext, guildId, channelId).ConfigureAwait(false);
                channelSettings.CurrentCount = currentCount;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                Log.Information("Current count updated for channel {ChannelId} in guild {GuildId}", channelId, guildId);
            }).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId)
        {
            Log.Information("Checking if channel {ChannelId} is a counting channel in guild {GuildId}", channelId, guildId);

            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    .ConfigureAwait(false);

                bool isCountingChannel = channelSettings != null;
                Log.Information("Channel {ChannelId} in guild {GuildId} is{Status} a counting channel.",
                    channelId, guildId, isCountingChannel ? "" : " not");
                return isCountingChannel;
            }).ConfigureAwait(false);
        }

        public async Task<int> GetChannelBase(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching base value for channel {ChannelId} in guild {GuildId}", channelId, guildId);

                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    .ConfigureAwait(false);

                int baseValue = channelSettings?.Base ?? 10;
                Log.Information("Base value for channel {ChannelId} is {BaseValue}", channelId, baseValue);
                return baseValue;
            }).ConfigureAwait(false);
        }

        public async Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using var dbContext = new BotDbContext();
                Log.Information("Fetching current count for channel {ChannelId} in guild {GuildId}", channelId, guildId);

                var channelSettings = await dbContext.ChannelSettings
                    .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                    .ConfigureAwait(false);

                if (channelSettings == null)
                {
                    Log.Warning("No settings found for channel {ChannelId} in guild {GuildId}. Returning 0.", channelId, guildId);
                    return 0;
                }

                Log.Information("Current count for channel {ChannelId} is {CurrentCount}", channelId, channelSettings.CurrentCount);
                return channelSettings.CurrentCount;
            }).ConfigureAwait(false);
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
                    .ConfigureAwait(false);

                var allChannels = await dbContext.ChannelSettings
                    .Where(c => c.GuildId == guildId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                foreach (var channel in allChannels)
                {
                    Log.Debug("Channel: {ChannelId}, Guild: {GuildId}, Name: {Name}, Base: {Base}, CurrentCount: {CurrentCount}", 
                        channel.ChannelId, channel.GuildId, channel.Name, channel.Base, channel.CurrentCount);
                }

                Log.Information("Found {ChannelCount} counting channels for guild {GuildId}", channels.Count, guildId);
                return channels;
            }).ConfigureAwait(false);
        }

        private async Task<GuildSettings> GetOrCreateGuildSettingsAsync(BotDbContext dbContext, ulong guildId)
        {
            var settings = await dbContext.GuildSettings.FindAsync(guildId).ConfigureAwait(false);
            if (settings == null)
            {
                Log.Information("No GuildSettings found for guild {GuildId}. Creating new settings entry.", guildId);
                settings = new GuildSettings { GuildId = guildId };
                dbContext.GuildSettings.Add(settings);
            }
            return settings;
        }

        private async Task<ChannelSettings> GetOrCreateChannelSettingsAsync(BotDbContext dbContext, ulong guildId, ulong channelId)
        {
            var channelSettings = await dbContext.ChannelSettings
                .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId)
                .ConfigureAwait(false);

            if (channelSettings == null)
            {
                Log.Information("No ChannelSettings found for channel {ChannelId} in guild {GuildId}. Creating new entry.", channelId, guildId);
                channelSettings = new ChannelSettings
                {
                    GuildId = guildId,
                    ChannelId = channelId
                };
                dbContext.ChannelSettings.Add(channelSettings);
            }
            return channelSettings;
        }
    }
}