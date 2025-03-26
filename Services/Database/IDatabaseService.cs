public interface IGuildSettingsService
{
    Task<string> GetPrefixAsync(ulong guildId);
    Task SetPrefixAsync(ulong guildId, string prefix);
}