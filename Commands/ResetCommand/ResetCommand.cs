using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("reset")]
        [AllowDMUsage]
        [Description("Resets your profile information.")]
        public async Task ResetCommandAsync(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(userId) ?? "en";

            string confirmationButton = await _languageService.GetLocalizedStringAsync(
                "ResetConfirmationButton",
                lang
            );
            string cancelationButton = await _languageService.GetLocalizedStringAsync(
                "ResetCancelationButton",
                lang
            );
            string confirmationMessage = await _languageService.GetLocalizedStringAsync(
                "ResetConfirmationMessage",
                lang
            );

            var confirmButton = new DiscordButtonComponent(
                DiscordButtonStyle.Danger,
                "confirm_reset",
                confirmationButton
            );
            var cancelButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                "cancel_reset",
                cancelationButton
            );
            var translateButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                $"translate_{null}_ResetConfirmationMessage",
                DiscordEmoji.FromUnicode("🌐")
            );

            var embed = new DiscordEmbedBuilder
            {
                Description = confirmationMessage,
                Color = DiscordColor.Orange,
            };

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AddComponents(confirmButton, cancelButton, translateButton)
                .AsEphemeral(true);

            await ctx.RespondAsync(responseBuilder);
        }
    }
}
