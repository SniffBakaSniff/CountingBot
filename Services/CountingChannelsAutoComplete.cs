using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

public class CountingChannelsAutoComplete : IAutoCompleteProvider
{
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        if (context.ServiceProvider.GetService(typeof(IGuildSettingsService)) is not IGuildSettingsService guildSettingsService)
            return Enumerable.Empty<DiscordAutoCompleteChoice>();

        var guildId = context.Guild!.Id;
        var channels = await guildSettingsService.GetCountingChannels(guildId) ?? [];

        string userInput = context.UserInput ?? string.Empty;

        var choices = channels
            .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && kvp.Key.StartsWith(userInput, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => new DiscordAutoCompleteChoice(kvp.Key, kvp.Value.ToString()))
            .ToList();

        return choices;
    }
}