using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace CountingBot.Listeners
{
    public class LeaveEventsHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;

        public LeaveEventsHandler(IGuildSettingsService guildSettingsService)
        {
            _guildSettingsService = guildSettingsService;
        }

        // Event is responsible for cleaning up data for when the bot is kicked from a server.
        public async Task HandleLeaveEvents(DiscordClient client, GuildDeletedEventArgs e)
        {
            await _guildSettingsService.DeleteGuildSettings(e.Guild.Id);
        }
    }
}
