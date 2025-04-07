using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("reset")]
        [Description("Resets your profile information.")]
        public async Task ResetCommandAsync(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;
            ulong guildId = ctx.Guild!.Id;
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(userId)
                            ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                            ?? "en";

            string confirmationButton = await _languageService.GetLocalizedStringAsync("ResetConfirmationButton", lang);
            string cancelationButton = await _languageService.GetLocalizedStringAsync("ResetCancelationButton", lang);
            string confirmationMessage = await _languageService.GetLocalizedStringAsync("ResetConfirmationMessage", lang);

            var confirmButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, "confirm_reset", confirmationButton);
            var cancelButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, "cancel_reset", cancelationButton);

            var embed = new DiscordEmbedBuilder
            {
                Description = confirmationMessage, 
                Color = DiscordColor.Orange
            };

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AddComponents(confirmButton, cancelButton)
                .AsEphemeral(true);

            await ctx.RespondAsync(responseBuilder);
        }
    }
}