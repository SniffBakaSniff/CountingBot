using CountingBot.Features.Commands;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;

namespace CountingBot.Listeners
{
    public class HelpComponentListener
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;

        public HelpComponentListener(
            IGuildSettingsService guildSettingsService,
            IUserInformationService userInformationService,
            ILanguageService languageService
        )
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
        }

        public async Task HandleComponentInteraction(
            DiscordClient client,
            ComponentInteractionCreatedEventArgs e
        )
        {
            // Check if this is a help section selector interaction
            if (e.Id == "help_section_selector")
            {
                await HandleHelpSectionSelection(e);
            }
        }

        private async Task HandleHelpSectionSelection(ComponentInteractionCreatedEventArgs e)
        {
            try
            {
                // Get the selected section
                string selectedSection = e.Values[0]; // The first (and only) selected value
                HelpSection section = Enum.Parse<HelpSection>(selectedSection);

                // Get user's preferred language
                string lang =
                    await _userInformationService.GetUserPreferredLanguageAsync(e.User.Id)
                    ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id)
                    ?? "en";

                // Create a new embed based on the selected section
                var embed = new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.Blurple)
                    .WithFooter($"Requested by {e.User.Username}", e.User.AvatarUrl)
                    .WithTimestamp(DateTime.UtcNow);

                string titleKey = string.Empty;
                string descriptionKey = string.Empty;

                // Build the appropriate embed based on the selected section
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

                // Create select menu for navigation with the current section selected
                string generalOption = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionGeneralOption",
                    lang
                );
                string generalDesc = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionGeneralDescription",
                    lang
                );
                string setupOption = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionSetupOption",
                    lang
                );
                string setupDesc = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionSetupDescription",
                    lang
                );
                string commandsOption = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCommandsOption",
                    lang
                );
                string commandsDesc = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCommandsDescription",
                    lang
                );
                string permissionsOption = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionPermissionsOption",
                    lang
                );
                string permissionsDesc = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionPermissionsDescription",
                    lang
                );
                string countingOption = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCountingOption",
                    lang
                );
                string countingDesc = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionCountingDescription",
                    lang
                );
                string selectorLabel = await _languageService.GetLocalizedStringAsync(
                    "HelpSectionSelectorLabel",
                    lang
                );

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

                // Create the message builder with the updated embed and components
                var messageBuilder = new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(selectMenu)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_{titleKey}_{descriptionKey}",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    );

                // Respond to the interaction with the updated message
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.UpdateMessage,
                    messageBuilder
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling help section selection");
                string errorMessage = await _languageService.GetLocalizedStringAsync(
                    "HelpComponentErrorMessage",
                    "en"
                );
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(errorMessage)
                        .AsEphemeral(true)
                );
            }
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
}
