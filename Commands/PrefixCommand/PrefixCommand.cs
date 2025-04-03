using DSharpPlus.Commands;
using CountingBot.Helpers;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("setprefix")]
        [System.ComponentModel.Description("Set the bot's prefix")]
        public async Task SetPrefixAsync(CommandContext ctx, string newPrefix)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                         ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                         ?? "en";

            if (string.IsNullOrWhiteSpace(newPrefix))
            {
                string emptyError = await _languageService.GetLocalizedStringAsync("PrefixEmptyError", lang);
                await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(emptyError));
                return;
            }

            if (newPrefix.Length > 3)
            {
                string longError = await _languageService.GetLocalizedStringAsync("PrefixTooLongError", lang);
                await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(longError));
                return;
            }

            newPrefix = newPrefix.ToLower();
            await _guildSettingsService.SetPrefixAsync(ctx.Guild!.Id, newPrefix);

            string successTitle = await _languageService.GetLocalizedStringAsync("PrefixUpdatedTitle", lang);
            string successDescTemplate = await _languageService.GetLocalizedStringAsync("PrefixUpdatedDescription", lang);
            string successDesc = string.Format(successDescTemplate, newPrefix);

            await ctx.RespondAsync(MessageHelpers.GenericSuccessEmbed(successTitle, successDesc));
        }
    }
}
