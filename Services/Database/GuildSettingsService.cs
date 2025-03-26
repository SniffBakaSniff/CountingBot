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
                    var settings = await GuildSettingsAsync(dbContext, guildId);
                    settings.Prefix = prefix;
                    await dbContext.SaveChangesAsync();
                }
            });
        }

        private async Task<GuildSettings> GuildSettingsAsync(BotDbContext dbContext, ulong guildId)
        {
            var settings = await dbContext.GuildSettings.FindAsync(guildId);

            if (settings is null)
            {
                settings = new GuildSettings { GuildId = guildId };
                dbContext.GuildSettings.Add(settings);
            }

            return settings;
        }
    }
}