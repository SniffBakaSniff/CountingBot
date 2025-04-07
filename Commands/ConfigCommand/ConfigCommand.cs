using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using CountingBot.Services;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("config")]
        public async Task ConfigCommandAsync(CommandContext ctx, Settings setting, string value)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                        ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id) ?? "en";

            string title = await _languageService.GetLocalizedStringAsync("ConfigUpdatedTitle", lang);
            string description;

            string titleKey = "Null";
            string descriptionKey;

            switch (setting)
            {
                case Settings.MathEnabled:
                    if (bool.TryParse(value, out bool isEnabled))
                    {
                        await _guildSettingsService.SetMathEnabledAsync(ctx.Guild!.Id, isEnabled);
                        string descriptionTemplate = await _languageService.GetLocalizedStringAsync("ConfigMathEnabledDescription", lang);
                        descriptionKey = "ConfigMathEnabledDescription";
                        description = string.Format(descriptionTemplate, isEnabled);
                    }
                    else
                    {
                        string errorTemplate = await _languageService.GetLocalizedStringAsync("InvalidBooleanValue", lang);
                        descriptionKey = "InvalidBooleanValue";
                        description = string.Format(errorTemplate, value);
                    }
                    break;

                case Settings.PreferredLanguage:
                    await _guildSettingsService.SetPreferedLanguageAsync(ctx.Guild!.Id, value);
                    string langDescriptionTemplate = await _languageService.GetLocalizedStringAsync("ConfigLanguageUpdatedDescription", lang);
                    descriptionKey = "ConfigLanguageUpdatedDescription";
                    description = string.Format(langDescriptionTemplate, value);
                    break;

                default:
                    string unknownSettingTemplate = await _languageService.GetLocalizedStringAsync("UnknownSetting", lang);
                    descriptionKey = "UnknownSetting";
                    description = string.Format(unknownSettingTemplate, setting);
                    break;
            }

            var embed = MessageHelpers.GenericUpdateEmbed(title, description);
            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed).AddComponents(
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_{titleKey}_{descriptionKey}", DiscordEmoji.FromUnicode("🌐"))
            ));
        }
    }

    public enum Settings
    {
        [ChoiceDisplayName("Math Enabled")]
        MathEnabled,

        [ChoiceDisplayName("Preferred Language")]
        PreferredLanguage
    }
}