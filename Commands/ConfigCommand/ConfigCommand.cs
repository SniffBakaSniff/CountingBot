using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        [Command("config")]
        public async Task ConfigCommandAsync(CommandContext ctx, Settings setting, bool enabled)
        {
            _ = _guildSettingsService.SetMathEnabledAsync(ctx.Guild!.Id, enabled);

            var embed = MessageHelpers.GenericUpdateEmbed($"Updated {setting}", $"{setting} : {enabled}");
            await ctx.RespondAsync(embed);
        }
    }

    public enum Settings
    {
        [ChoiceDisplayName("Math Enabled")]
        MathEnabled
    }
}