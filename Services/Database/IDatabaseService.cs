using CountingBot.Database;


namespace CountingBot.Services.Database
{
    public interface IGuildSettingsService
    {
        Task<string> GetPrefixAsync(ulong guildId);
        Task SetPrefixAsync(ulong guildId, string prefix);
        Task SetCountingChannel(ulong guildId, ulong channelId, int baseValue, string name);
        Task<bool> CheckIfCountingChannel(ulong guildId, ulong channelId);
        Task<int> GetChannelBase(ulong guildId, ulong channelId);
        Task SetChannelsCurrentCount(ulong guildId, ulong channelId, int currentCount);
        Task<int> GetChannelsCurrentCount(ulong guildId, ulong channelId);
        Task<Dictionary<string, ulong>> GetCountingChannels(ulong guildId);
    }

    public interface IUserInformationService
    {
        Task<UserInformation> GetUserInfoAsync(ulong userId);
        Task UpdateUserCountAsync(ulong guildId, ulong userId, int currentCount, bool correctCount);
    }
}