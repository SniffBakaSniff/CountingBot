using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;

public class CustomPrefixResolver : IPrefixResolver
{
    private readonly IGuildSettingsService _guildSettingsService;

    public CustomPrefixResolver(IGuildSettingsService guildSettingsService)
    {
        _guildSettingsService = guildSettingsService;
    }

    public async ValueTask<int> ResolvePrefixAsync(CommandsExtension extension, DiscordMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content) || message.Channel is null)
        {
            return -1;
        }

        if (message.Channel.GuildId.HasValue)
        {
            var guildId = message.Channel.GuildId.Value;
            var prefix = await _guildSettingsService.GetPrefixAsync(guildId);
            if (message.Content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return prefix.Length;
            }
        }

        return -1;
    }
}