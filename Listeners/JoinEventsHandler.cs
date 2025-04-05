using DSharpPlus;
using DSharpPlus.EventArgs;
using CountingBot.Services.Database;

namespace CountingBot.Listeners
{
    public class JoinEventsHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        public JoinEventsHandler(IGuildSettingsService guildSettingsService)
        {
            _guildSettingsService = guildSettingsService;
        }

        public async Task HandleJoinEvents(DiscordClient client, GuildCreatedEventArgs e)
        {
            var guildSettings = await _guildSettingsService.GetOrCreateGuildSettingsAsync(e.Guild.Id);

            if (guildSettings is not null)
            {
                return;
            }
        }
        
    }
}
