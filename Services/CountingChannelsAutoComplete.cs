using CountingBot.Services.Database;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CountingBot.Services
{
    /// <summary>
    /// Provides autocomplete functionality for counting channel selection in slash commands.
    /// Implements DSharpPlus's IAutoCompleteProvider interface to suggest available counting channels.
    /// </summary>
    public class CountingChannelsAutoComplete : IAutoCompleteProvider
    {
        /// <summary>
        /// Generates autocomplete choices based on the user's input and available counting channels.
        /// Filters channels based on the user's partial input for a more refined suggestion list.
        /// </summary>
        /// <param name="context">The autocomplete context containing user input and guild information</param>
        /// <returns>Collection of autocomplete choices matching the user's input</returns>
        public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(
            AutoCompleteContext context
        )
        {
            if (
                context.ServiceProvider.GetService(typeof(IGuildSettingsService))
                is not IGuildSettingsService guildSettingsService
            )
                return Enumerable.Empty<DiscordAutoCompleteChoice>();

            var guildId = context.Guild!.Id;
            var channels = await guildSettingsService.GetCountingChannels(guildId) ?? [];

            string userInput = context.UserInput ?? string.Empty;

            var choices = channels
                .Where(kvp =>
                    !string.IsNullOrEmpty(kvp.Key)
                    && kvp.Key.StartsWith(userInput, StringComparison.OrdinalIgnoreCase)
                )
                .Select(kvp => new DiscordAutoCompleteChoice(kvp.Key, kvp.Value.ToString()))
                .ToList();

            return choices;
        }
    }
}
