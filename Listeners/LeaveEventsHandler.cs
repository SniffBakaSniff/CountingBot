using DSharpPlus;
using DSharpPlus.EventArgs;
using CountingBot.Services.Database;

namespace CountingBot.Listeners
{
    public class LeaveEventsHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        public LeaveEventsHandler(IGuildSettingsService guildSettingsService)
        {
            _guildSettingsService = guildSettingsService;
        }

        public async Task HandleLeaveEvents(DiscordClient client, GuildDeletedEventArgs e)
        {
            await _guildSettingsService.DeleteGuildSettings(e.Guild.Id);
        }
    }
}
