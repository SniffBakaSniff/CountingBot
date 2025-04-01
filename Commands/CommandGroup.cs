using CountingBot.Services.Database;
namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInforamtionService;

        public CommandsGroup(IGuildSettingsService guildSettingsService, IUserInformationService userInformationService)
        {
            _guildSettingsService = guildSettingsService;
            _userInforamtionService = userInformationService;
        }
    }
}