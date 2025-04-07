using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("language")]
        public async Task LanguageCommandAsync(CommandContext ctx, SupportedLanguage language)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                        ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id) ?? "en";

            try
            {
                string selectedLanguage = language.ToString().ToLower();
                await _userInformationService.SetPreferredLanguageAsync(ctx.User.Id, selectedLanguage);
                string title = await _languageService.GetLocalizedStringAsync("ConfigUpdatedTitle", lang);
                string titleKey = "ConfigUpdatedTitle";
                string descriptionTemplate = await _languageService.GetLocalizedStringAsync("ConfigLanguageUpdatedDescription", lang);
                string descriptionKey = "ConfigLanguageUpdatedDescription";
                string description = string.Format(descriptionTemplate, selectedLanguage);
                var embed = MessageHelpers.GenericUpdateEmbed(title, description);
                
                await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_{titleKey}_{descriptionKey}", DiscordEmoji.FromUnicode("🌐"))
                ));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update language for user {UserId}", ctx.User.Id);
                string errorTitle = await _languageService.GetLocalizedStringAsync("GenericErrorTitle", lang);
                string titleKey = "GenericErrorTitle";
                string errorMessage = await _languageService.GetLocalizedStringAsync("GenericErrorMessage", lang);
                string descriptionKey = "GenericErrorMessage";
                var errorEmbed = MessageHelpers.GenericErrorEmbed(errorTitle, errorMessage);
                await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(errorEmbed).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_{titleKey}_{descriptionKey}", DiscordEmoji.FromUnicode("🌐"))
                ));
            }
        }
    }

    public enum SupportedLanguage
    {
        [ChoiceDisplayName("English")]
        en,
        [ChoiceDisplayName("Weeblish")]
        weeblish,
        [ChoiceDisplayName("Trollish")]
        trollish
    }
}