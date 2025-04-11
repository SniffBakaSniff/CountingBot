using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("help")]
        [AllowDMUsage]
        [Description("Displays helpful information about the bot and its commands.")]
        public async Task HelpCommandAsync(
            CommandContext ctx,
            HelpSection section = HelpSection.General
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Blurple)
                .WithFooter($"Requested by {ctx.User.Username}", ctx.User.AvatarUrl)
                .WithTimestamp(DateTime.UtcNow);

            string titleKey = string.Empty;
            string descriptionKey = string.Empty;

            switch (section)
            {
                case HelpSection.General:
                    await BuildGeneralHelpEmbed(embed, lang);
                    titleKey = "HelpGeneralTitle";
                    descriptionKey = "HelpGeneralDescription";
                    break;

                case HelpSection.Setup:
                    await BuildSetupHelpEmbed(embed, lang);
                    titleKey = "HelpSetupTitle";
                    descriptionKey = "HelpSetupDescription";
                    break;

                case HelpSection.Commands:
                    await BuildCommandsHelpEmbed(embed, lang);
                    titleKey = "HelpCommandsTitle";
                    descriptionKey = "HelpCommandsDescription";
                    break;

                case HelpSection.Permissions:
                    await BuildPermissionsHelpEmbed(embed, lang);
                    titleKey = "HelpPermissionsTitle";
                    descriptionKey = "HelpPermissionsDescription";
                    break;

                case HelpSection.Counting:
                    await BuildCountingHelpEmbed(embed, lang);
                    titleKey = "HelpCountingTitle";
                    descriptionKey = "HelpCountingDescription";
                    break;
            }

            // Create select menu for navigation
            string generalOption =
                await _languageService.GetLocalizedStringAsync("HelpSectionGeneralOption", lang)
                ?? "General Information";
            string generalDesc =
                await _languageService.GetLocalizedStringAsync(
                    "HelpSectionGeneralDescription",
                    lang
                ) ?? "General bot information";
            string setupOption =
                await _languageService.GetLocalizedStringAsync("HelpSectionSetupOption", lang)
                ?? "Setup Guide";
            string setupDesc =
                await _languageService.GetLocalizedStringAsync("HelpSectionSetupDescription", lang)
                ?? "How to set up the bot";
            string commandsOption =
                await _languageService.GetLocalizedStringAsync("HelpSectionCommandsOption", lang)
                ?? "Commands List";
            string commandsDesc =
                await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCommandsDescription",
                    lang
                ) ?? "List of available commands";
            string permissionsOption =
                await _languageService.GetLocalizedStringAsync("HelpSectionPermissionsOption", lang)
                ?? "Permissions";
            string permissionsDesc =
                await _languageService.GetLocalizedStringAsync(
                    "HelpSectionPermissionsDescription",
                    lang
                ) ?? "How to manage command permissions";
            string countingOption =
                await _languageService.GetLocalizedStringAsync("HelpSectionCountingOption", lang)
                ?? "Counting Guide";
            string countingDesc =
                await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCountingDescription",
                    lang
                ) ?? "How counting works";
            string selectorLabel =
                await _languageService.GetLocalizedStringAsync("HelpSectionSelectorLabel", lang)
                ?? "Select Help Section";

            var options = new List<DiscordSelectComponentOption>
            {
                new DiscordSelectComponentOption(
                    generalOption,
                    HelpSection.General.ToString(),
                    generalDesc,
                    isDefault: section == HelpSection.General
                ),
                new DiscordSelectComponentOption(
                    setupOption,
                    HelpSection.Setup.ToString(),
                    setupDesc,
                    isDefault: section == HelpSection.Setup
                ),
                new DiscordSelectComponentOption(
                    commandsOption,
                    HelpSection.Commands.ToString(),
                    commandsDesc,
                    isDefault: section == HelpSection.Commands
                ),
                new DiscordSelectComponentOption(
                    permissionsOption,
                    HelpSection.Permissions.ToString(),
                    permissionsDesc,
                    isDefault: section == HelpSection.Permissions
                ),
                new DiscordSelectComponentOption(
                    countingOption,
                    HelpSection.Counting.ToString(),
                    countingDesc,
                    isDefault: section == HelpSection.Counting
                ),
            };

            var selectMenu = new DiscordSelectComponent(
                "help_section_selector",
                selectorLabel,
                options
            );

            await ctx.RespondAsync(
                new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddComponents(selectMenu)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_{titleKey}_{descriptionKey}",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    )
            );
        }

        private async Task BuildGeneralHelpEmbed(DiscordEmbedBuilder embed, string lang)
        {
            string title = await _languageService.GetLocalizedStringAsync("HelpGeneralTitle", lang);
            string description = await _languageService.GetLocalizedStringAsync(
                "HelpGeneralDescription",
                lang
            );

            embed.WithTitle(title).WithDescription(description);

            string featuresTitle = await _languageService.GetLocalizedStringAsync(
                "HelpGeneralFeaturesTitle",
                lang
            );
            string featuresContent = await _languageService.GetLocalizedStringAsync(
                "HelpGeneralFeaturesContent",
                lang
            );

            embed.AddField(featuresTitle, featuresContent, false);

            string supportTitle = await _languageService.GetLocalizedStringAsync(
                "HelpGeneralSupportTitle",
                lang
            );
            string supportContent = await _languageService.GetLocalizedStringAsync(
                "HelpGeneralSupportContent",
                lang
            );

            embed.AddField(supportTitle, supportContent, false);
        }

        private async Task BuildSetupHelpEmbed(DiscordEmbedBuilder embed, string lang)
        {
            string title = await _languageService.GetLocalizedStringAsync("HelpSetupTitle", lang);
            string description = await _languageService.GetLocalizedStringAsync(
                "HelpSetupDescription",
                lang
            );

            embed.WithTitle(title).WithDescription(description);

            // Step 1: Create a counting channel
            string step1Title = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep1Title",
                lang
            );
            string step1Content = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep1Content",
                lang
            );

            embed.AddField(step1Title, step1Content, false);

            // Step 2: Configure settings
            string step2Title = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep2Title",
                lang
            );
            string step2Content = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep2Content",
                lang
            );

            embed.AddField(step2Title, step2Content, false);

            // Step 3: Set permissions
            string step3Title = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep3Title",
                lang
            );
            string step3Content = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep3Content",
                lang
            );

            embed.AddField(step3Title, step3Content, false);

            // Step 4: Start counting
            string step4Title = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep4Title",
                lang
            );
            string step4Content = await _languageService.GetLocalizedStringAsync(
                "HelpSetupStep4Content",
                lang
            );

            embed.AddField(step4Title, step4Content, false);
        }

        private async Task BuildCommandsHelpEmbed(DiscordEmbedBuilder embed, string lang)
        {
            string title = await _languageService.GetLocalizedStringAsync(
                "HelpCommandsTitle",
                lang
            );
            string description = await _languageService.GetLocalizedStringAsync(
                "HelpCommandsDescription",
                lang
            );

            embed.WithTitle(title).WithDescription(description);

            // User Commands
            string userCommandsTitle = await _languageService.GetLocalizedStringAsync(
                "HelpUserCommandsTitle",
                lang
            );
            string userCommandsContent = await _languageService.GetLocalizedStringAsync(
                "HelpUserCommandsContent",
                lang
            );

            embed.AddField(userCommandsTitle, userCommandsContent, false);

            // Admin Commands
            string adminCommandsTitle = await _languageService.GetLocalizedStringAsync(
                "HelpAdminCommandsTitle",
                lang
            );
            string adminCommandsContent = await _languageService.GetLocalizedStringAsync(
                "HelpAdminCommandsContent",
                lang
            );

            embed.AddField(adminCommandsTitle, adminCommandsContent, false);

            // Permission Commands
            string permissionCommandsTitle = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionCommandsTitle",
                lang
            );
            string permissionCommandsContent = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionCommandsContent",
                lang
            );

            embed.AddField(permissionCommandsTitle, permissionCommandsContent, false);
        }

        private async Task BuildPermissionsHelpEmbed(DiscordEmbedBuilder embed, string lang)
        {
            string title = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsTitle",
                lang
            );
            string description = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsDescription",
                lang
            );

            embed.WithTitle(title).WithDescription(description);

            // Permission Basics
            string basicsTitle = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsBasicsTitle",
                lang
            );
            string basicsContent = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsBasicsContent",
                lang
            );

            embed.AddField(basicsTitle, basicsContent, false);

            // Setting Permissions
            string settingTitle = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsSettingTitle",
                lang
            );
            string settingContent = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsSettingContent",
                lang
            );

            embed.AddField(settingTitle, settingContent, false);

            // Blacklisting
            string blacklistTitle = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsBlacklistTitle",
                lang
            );
            string blacklistContent = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsBlacklistContent",
                lang
            );

            embed.AddField(blacklistTitle, blacklistContent, false);

            // Managing Permissions
            string managingTitle = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsManagingTitle",
                lang
            );
            string managingContent = await _languageService.GetLocalizedStringAsync(
                "HelpPermissionsManagingContent",
                lang
            );

            embed.AddField(managingTitle, managingContent, false);
        }

        private async Task BuildCountingHelpEmbed(DiscordEmbedBuilder embed, string lang)
        {
            string title = await _languageService.GetLocalizedStringAsync(
                "HelpCountingTitle",
                lang
            );
            string description = await _languageService.GetLocalizedStringAsync(
                "HelpCountingDescription",
                lang
            );

            embed.WithTitle(title).WithDescription(description);

            // Basic Rules
            string rulesTitle = await _languageService.GetLocalizedStringAsync(
                "HelpCountingRulesTitle",
                lang
            );
            string rulesContent = await _languageService.GetLocalizedStringAsync(
                "HelpCountingRulesContent",
                lang
            );

            embed.AddField(rulesTitle, rulesContent, false);

            // Number Systems
            string systemsTitle = await _languageService.GetLocalizedStringAsync(
                "HelpCountingSystemsTitle",
                lang
            );
            string systemsContent = await _languageService.GetLocalizedStringAsync(
                "HelpCountingSystemsContent",
                lang
            );

            embed.AddField(systemsTitle, systemsContent, false);

            // Math Expressions
            string mathTitle = await _languageService.GetLocalizedStringAsync(
                "HelpCountingMathTitle",
                lang
            );
            string mathContent = await _languageService.GetLocalizedStringAsync(
                "HelpCountingMathContent",
                lang
            );

            embed.AddField(mathTitle, mathContent, false);

            // Revives
            string revivesTitle = await _languageService.GetLocalizedStringAsync(
                "HelpCountingRevivesTitle",
                lang
            );
            string revivesContent = await _languageService.GetLocalizedStringAsync(
                "HelpCountingRevivesContent",
                lang
            );

            embed.AddField(revivesTitle, revivesContent, false);
        }
    }

    [Description("Help sections available in the help command")]
    public enum HelpSection
    {
        [ChoiceDisplayName("General Information")]
        [Description("General bot information and features")]
        General,

        [ChoiceDisplayName("Setup Guide")]
        [Description("How to set up the bot in your server")]
        Setup,

        [ChoiceDisplayName("Commands List")]
        [Description("List of available commands")]
        Commands,

        [ChoiceDisplayName("Permissions Guide")]
        [Description("How to manage command permissions")]
        Permissions,

        [ChoiceDisplayName("Counting Guide")]
        [Description("How counting works and rules to follow")]
        Counting,
    }
}
