using CountingBot.Database.Models;

namespace CountingBot.Services.Database
{
    public interface IGuildSettingsService
    {
        Task<string> GetPrefixAsync(ulong guildId);
        Task<bool> GetMathEnabledAsync(ulong guildId);
        Task SetPrefixAsync(ulong guildId, string prefix);
        Task SetMathEnabledAsync(ulong guildId, bool enabled);
        Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name);
        Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId);
        Task<int> GetChannelBase(ulong guildId, ulong channelId);
        Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount);
        Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId);
        Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId);
        Task<string> GetGuildPreferredLanguageAsync(ulong guildId);
        Task SetPreferedLanguageAsync(ulong guildId, string language);
    }

    public interface IUserInformationService
    {
        Task<UserInformation> GetUserInformationAsync(ulong userId);
        Task UpdateUserCountAsync(ulong guildId, ulong userId, int currentCount, bool correctCount);
        Task<bool> GetUserRevivesAsync(ulong userId, bool removeRevive);
        Task<string> GetUserPreferredLanguageAsync(ulong userId);
        Task SetPreferredLanguageAsync(ulong userId, string language);
    }
}