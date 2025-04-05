using CountingBot.Services;
using CountingBot.Services.Database;
namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;
        private readonly ILeaderboardService _leaderboardService;

        public CommandsGroup(IGuildSettingsService guildSettingsService,
                             IUserInformationService userInformationService,
                             ILanguageService languageService,
                             ILeaderboardService leaderboardService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
            _leaderboardService = leaderboardService;
        }
    }
}