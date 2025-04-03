using CountingBot.Services;
using CountingBot.Services.Database;
namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;

        public CommandsGroup(IGuildSettingsService guildSettingsService,
                             IUserInformationService userInformationService,
                             ILanguageService languageService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
        }
    }
}