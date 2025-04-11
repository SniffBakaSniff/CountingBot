using System.ComponentModel;
using CountingBot.Features.Attributes;
using CountingBot.Helpers;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    [Command("config")]
    [Description("Configure bot settings for your server.")]
    public class ConfigCommands
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;

        public ConfigCommands(
            IGuildSettingsService guildSettingsService,
            IUserInformationService userInformationService,
            ILanguageService languageService
        )
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
        }

        [Command("view")]
        [Description("View current configuration settings.")]
        [PermissionCheck("config_command")]
        public async Task ViewConfigAsync(CommandContext ctx)
        {
            ulong guildId = ctx.Guild!.Id;
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";

            try
            {
                // Fetch current settings
                bool mathEnabled = await _guildSettingsService.GetMathEnabledAsync(guildId);
                string language =
                    await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                string prefix = await _guildSettingsService.GetPrefixAsync(guildId);

                // Get localized strings
                string title = await _languageService.GetLocalizedStringAsync(
                    "ConfigViewTitle",
                    lang
                );
                string mathEnabledLabel = await _languageService.GetLocalizedStringAsync(
                    "ConfigMathEnabledLabel",
                    lang
                );
                string languageLabel = await _languageService.GetLocalizedStringAsync(
                    "ConfigLanguageLabel",
                    lang
                );
                string prefixLabel = await _languageService.GetLocalizedStringAsync(
                    "ConfigPrefixLabel",
                    lang
                );

                // Create an embed showing all settings
                var embed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Blurple)
                    .AddField(mathEnabledLabel, mathEnabled ? "✅ Enabled" : "❌ Disabled", true)
                    .AddField(languageLabel, language, true)
                    .AddField(prefixLabel, prefix, true);

                await ctx.RespondAsync(
                    new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_ConfigViewTitle_Original",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );

                Log.Information("Displayed configuration settings for guild {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error displaying configuration for guild {GuildId}", guildId);
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder().WithContent(errorMsg).AsEphemeral(true)
                );
            }
        }

        [Command("math")]
        [Description("Enable or disable math expressions in counting.")]
        [PermissionCheck("config_command")]
        public async Task ConfigMathAsync(
            CommandContext ctx,
            [Description("Whether math expressions should be enabled.")] bool enabled
        )
        {
            ulong guildId = ctx.Guild!.Id;
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";

            try
            {
                // Get the current setting before changing it
                bool currentSetting = await _guildSettingsService.GetMathEnabledAsync(guildId);

                // Only update if the setting is actually changing
                if (currentSetting != enabled)
                {
                    await _guildSettingsService.SetMathEnabledAsync(guildId, enabled);
                    Log.Information(
                        "Updated math enabled setting for guild {GuildId} from {OldValue} to {NewValue}",
                        guildId,
                        currentSetting,
                        enabled
                    );
                }

                // Get localized strings
                string title = await _languageService.GetLocalizedStringAsync(
                    "ConfigUpdatedTitle",
                    lang
                );
                string settingName = await _languageService.GetLocalizedStringAsync(
                    "ConfigMathSettingName",
                    lang
                );
                string oldValue = await _languageService.GetLocalizedStringAsync(
                    currentSetting ? "Enabled" : "Disabled",
                    lang
                );
                string newValue = await _languageService.GetLocalizedStringAsync(
                    enabled ? "Enabled" : "Disabled",
                    lang
                );

                // Create response embed
                var embed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Green)
                    .AddField(settingName, $"{oldValue} → {newValue}");

                await ctx.RespondAsync(
                    new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_ConfigUpdatedTitle_Original",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating math setting for guild {GuildId}", guildId);
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder().WithContent(errorMsg).AsEphemeral(true)
                );
            }
        }

        [Command("language")]
        [Description("Set the preferred language for the server.")]
        [PermissionCheck("config_command")]
        public async Task ConfigLanguageAsync(
            CommandContext ctx,
            [Description("The language to use.")] SupportedLanguage language
        )
        {
            ulong guildId = ctx.Guild!.Id;
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";

            try
            {
                // Get the current setting before changing it
                string currentLanguage =
                    await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                string newLanguage = language.ToString().ToLower();

                // Only update if the setting is actually changing
                if (currentLanguage != newLanguage)
                {
                    await _guildSettingsService.SetPreferedLanguageAsync(guildId, newLanguage);
                    Log.Information(
                        "Updated preferred language for guild {GuildId} from {OldValue} to {NewValue}",
                        guildId,
                        currentLanguage,
                        newLanguage
                    );
                }

                // Get localized strings
                string title = await _languageService.GetLocalizedStringAsync(
                    "ConfigUpdatedTitle",
                    lang
                );
                string settingName = await _languageService.GetLocalizedStringAsync(
                    "ConfigLanguageSettingName",
                    lang
                );

                // Create response embed
                var embed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Green)
                    .AddField(settingName, $"{currentLanguage} → {newLanguage}");

                await ctx.RespondAsync(
                    new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_ConfigUpdatedTitle_Original",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating language setting for guild {GuildId}", guildId);
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder().WithContent(errorMsg).AsEphemeral(true)
                );
            }
        }

        [Command("prefix")]
        [Description("Set the command prefix for the server.")]
        [PermissionCheck("config_command")]
        public async Task ConfigPrefixAsync(
            CommandContext ctx,
            [Description("The new prefix to use.")] string newPrefix
        )
        {
            ulong guildId = ctx.Guild!.Id;
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";

            try
            {
                // Validate the prefix
                if (string.IsNullOrWhiteSpace(newPrefix))
                {
                    string errorMsg = await _languageService.GetLocalizedStringAsync(
                        "PrefixEmptyError",
                        lang
                    );
                    await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(errorMsg));
                    return;
                }

                if (newPrefix.Length > 3)
                {
                    string errorMsg = await _languageService.GetLocalizedStringAsync(
                        "PrefixTooLongError",
                        lang
                    );
                    await ctx.RespondAsync(MessageHelpers.GenericErrorEmbed(errorMsg));
                    return;
                }

                // Get the current setting before changing it
                string currentPrefix = await _guildSettingsService.GetPrefixAsync(guildId);

                // Only update if the setting is actually changing
                if (currentPrefix != newPrefix)
                {
                    await _guildSettingsService.SetPrefixAsync(guildId, newPrefix);
                    Log.Information(
                        "Updated prefix for guild {GuildId} from {OldValue} to {NewValue}",
                        guildId,
                        currentPrefix,
                        newPrefix
                    );
                }

                // Get localized strings
                string title = await _languageService.GetLocalizedStringAsync(
                    "ConfigUpdatedTitle",
                    lang
                );
                string settingName = await _languageService.GetLocalizedStringAsync(
                    "ConfigPrefixSettingName",
                    lang
                );

                // Create response embed
                var embed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Green)
                    .AddField(settingName, $"`{currentPrefix}` → `{newPrefix}`");

                await ctx.RespondAsync(
                    new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_ConfigUpdatedTitle_Original",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating prefix setting for guild {GuildId}", guildId);
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder().WithContent(errorMsg).AsEphemeral(true)
                );
            }
        }
    }
}
