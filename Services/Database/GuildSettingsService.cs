using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CountingBot.Services.Database
{
    public class GuildSettingsService : IGuildSettingsService
    {
        public async Task<string> GetPrefixAsync(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Fetching prefix for guild {GuildId}", guildId);
                    var settings = await dbContext.GuildSettings.FindAsync(guildId);
                    return settings?.Prefix ?? "!";
                }
            });
        }

        public async Task SetPrefixAsync(ulong guildId, string prefix)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Setting prefix for guild {GuildId} to {Prefix}", guildId, prefix);
                    var settings = await GuildSettingsAsync(dbContext, guildId);
                    settings.Prefix = prefix;
                    await dbContext.SaveChangesAsync();
                    Log.Information("Prefix updated successfully for guild {GuildId}", guildId);
                }
            });
        }

        public async Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Fetching ChannelSettings for GuildId: {GuildId}, ChannelId: {ChannelId}", guildId, channelId);
                    
                    var channelSettings = await dbContext.ChannelSettings
                        .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId);

                    if (channelSettings == null)
                    {
                        Log.Information("Adding new counting channel {ChannelId} with base value {BaseValue} for GuildId: {GuildId}.", channelId, baseValue, guildId);
                        channelSettings = new ChannelSettings
                        {
                            GuildId = guildId,
                            ChannelId = channelId,
                            Base = baseValue,
                            Name = name
                        };
                        dbContext.ChannelSettings.Add(channelSettings);
                    }
                    else
                    {
                        Log.Information("Updating base value of existing counting channel {ChannelId} to {BaseValue} for GuildId: {GuildId}.", channelId, baseValue, guildId);
                        channelSettings.Base = baseValue;
                        channelSettings.Name = name;
                    }

                    await dbContext.SaveChangesAsync();
                    Log.Information("Successfully saved changes for GuildId: {GuildId}, ChannelId: {ChannelId}.", guildId, channelId);
                }
            });
        }

        public async Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount)
        {
            await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Setting current count for channel {ChannelId} in guild {GuildId} to {CurrentCount}", channelId, guildId, currentCount);
                    
                    var channelSettings = await dbContext.ChannelSettings
                        .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId);
                    
                    if (channelSettings == null)
                    {
                        Log.Warning("No channel settings found for channel {ChannelId} in guild {GuildId}. Creating a new entry.", channelId, guildId);
                        channelSettings = new ChannelSettings
                        {
                            ChannelId = channelId,
                            GuildId = guildId,
                            CurrentCount = currentCount
                        };
                        dbContext.ChannelSettings.Add(channelSettings);
                    }
                    else
                    {
                        channelSettings.CurrentCount = currentCount;
                    }
                    
                    await dbContext.SaveChangesAsync();
                    Log.Information("Successfully updated current count for channel {ChannelId} in guild {GuildId}.", channelId, guildId);
                }
            });
        }

        public async Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId)
        {
            Log.Information("Checking if channel {ChannelId} is a counting channel in guild {GuildId}...", channelId, guildId);
            
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Debug("Retrieving channel settings for channel {ChannelId} in guild {GuildId} from the database...", channelId, guildId);
                    var channelSettings = await dbContext.ChannelSettings
                        .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId);

                    bool isCountingChannel = channelSettings != null;
                    Log.Information("Channel {ChannelId} in guild {GuildId} is{IsCountingChannel} a counting channel.",
                        channelId, guildId, isCountingChannel ? "" : " not");

                    return isCountingChannel;
                }
            });
        }

        public async Task<int> GetChannelBase(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Fetching base value for channel {ChannelId} in guild {GuildId}", channelId, guildId);
                    var channelSettings = await dbContext.ChannelSettings
                        .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId);

                    var baseValue = channelSettings?.Base ?? 10;
                    Log.Information("Base value for channel {ChannelId}: {BaseValue}", channelId, baseValue);
                    return baseValue;
                }
            });
        }

        public async Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Fetching current count for channel {ChannelId} in guild {GuildId}", channelId, guildId);
                    
                    var channelSettings = await dbContext.ChannelSettings
                        .SingleOrDefaultAsync(c => c.ChannelId == channelId && c.GuildId == guildId);
                    
                    if (channelSettings == null)
                    {
                        Log.Warning("No channel settings found for channel {ChannelId} in guild {GuildId}. Returning 0.", channelId, guildId);
                        return 0;
                    }
                    
                    int currentCount = channelSettings.CurrentCount;
                    Log.Information("Fetched current count {CurrentCount} for channel {ChannelId} in guild {GuildId}", currentCount, channelId, guildId);
                    return currentCount;
                }
            });
        }

        public async Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId)
        {
            return await ExceptionHandler.HandleAsync(async () =>
            {
                using (var dbContext = new BotDbContext())
                {
                    Log.Information("Fetching all counting channels for guild {GuildId}", guildId);

                    try
                    {
                        var channels = await dbContext.ChannelSettings
                        .Where(c => c.GuildId == guildId)
                        .Select(c => new { Name = c.Name ?? c.ChannelId.ToString(), c.ChannelId })
                        .ToDictionaryAsync(c => c.Name, c => c.ChannelId);

                        var allChannels = await dbContext.ChannelSettings
                        .Where(c => c.GuildId == guildId)
                        .ToListAsync();

                        foreach (var channel in allChannels)
                        {
                            Log.Debug("ChannelSettings: {ChannelId}, {GuildId}, {Name}, {Base}, {CurrentCount}", 
                                channel.ChannelId, 
                                channel.GuildId, 
                                channel.Name, 
                                channel.Base, 
                                channel.CurrentCount);
                        }

                        Log.Information("Found {ChannelCount} counting channels for guild {GuildId}", channels.Count, guildId);
                        return channels;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An error occurred while fetching counting channels for guild {GuildId}", guildId);
                        throw;
                    }

                }
            });
        }


        private async Task<GuildSettings> GuildSettingsAsync(BotDbContext dbContext, ulong guildId)
        {
            var settings = await dbContext.GuildSettings.FindAsync(guildId);

            if (settings is null)
            {
                Log.Information("Guild settings not found for {GuildId}, creating new settings entry.", guildId);
                settings = new GuildSettings { GuildId = guildId };
                dbContext.GuildSettings.Add(settings);
            }

            return settings;
        }
    }
}