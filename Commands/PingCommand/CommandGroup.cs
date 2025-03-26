
namespace CountingBot.Features.ConfigCommands
{
    public partial class CommandsGroup
    {

        private readonly IGuildSettingsService _guildSettingsService;
        private readonly BotDbContext dbContext = new BotDbContext();

        public CommandsGroup(IGuildSettingsService guildSettingsService)
        {
            _guildSettingsService = guildSettingsService;
        }
    }
}