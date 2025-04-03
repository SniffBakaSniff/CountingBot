using DSharpPlus.Commands;
using CountingBot.Helpers;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("setup")]
        [System.ComponentModel.Description("Set up a counting channel")]
        public async Task SetupAsync(CommandContext ctx, NumberSystem? type, DiscordChannel? channel)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                         ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                         ?? "en";

            if (type is null || channel is null)
            {
                string errorMessage = await _languageService.GetLocalizedStringAsync("SetupInvalidInput", lang);
                await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(errorMessage));
                return;
            }

            if (channel!.Type != DiscordChannelType.Text)
            {
                string errorMessage = await _languageService.GetLocalizedStringAsync("SetupInvalidChannel", lang);
                await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(errorMessage));
                return;
            }

            int baseValue = (int)type.Value;

            Log.Information("Setting up counting channel {ChannelId} in {GuildId} with base {Base}.", channel.Id, ctx.Guild!.Id, baseValue);

            await _guildSettingsService.SetCountingChannel(ctx.Guild.Id, channel.Id, baseValue, channel.Name);

            string successTitle = await _languageService.GetLocalizedStringAsync("SetupSuccessTitle", lang);
            string successDescTemplate = await _languageService.GetLocalizedStringAsync("SetupSuccessDescription", lang);
            string successDesc = string.Format(successDescTemplate, channel.Mention, baseValue);

            await ctx.RespondAsync(MessageHelpers.GenericSuccessEmbed(successTitle, successDesc));

            Log.Information("Counting channel {ChannelId} successfully set up in {GuildId} with base {Base}.", channel.Id, ctx.Guild!.Id, baseValue);
        }
    }

    public enum NumberSystem
    {
        [ChoiceDisplayName("Binary (Base-2)")]
        Binary = 2,

        [ChoiceDisplayName("Octal (Base-8)")]
        Octal = 8,

        [ChoiceDisplayName("Decimal (Base-10)")]
        Decimal = 10,

        [ChoiceDisplayName("Hexadecimal (Base-16)")]
        Hexadecimal = 16
    }
}
