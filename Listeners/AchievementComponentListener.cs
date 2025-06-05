using CountingBot.Features.Commands;
using CountingBot.Helpers;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;

namespace CountingBot.Listeners
{
    public class AchievementComponentListener
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;

        public AchievementComponentListener(
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
            // Check if this is an achievement type selector interaction
            if (e.Id == "achievement_type_selector")
            {
                await HandleAchievementTypeSelection(e);
            }
        }

        private async Task HandleAchievementTypeSelection(ComponentInteractionCreatedEventArgs e)
        {
            try
            {
                // Get the selected type
                string selectedType = e.Values[0]; // The first (and only) selected value
                AchievementType type = Enum.Parse<AchievementType>(selectedType);

                // Get user's preferred language
                string lang =
                    await _userInformationService.GetUserPreferredLanguageAsync(e.User.Id)
                    ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id)
                    ?? "en";

                // Get all achievements
                var allAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                    e.User.Id,
                    1,
                    pageSize: 50
                );

                // Filter achievements by type if needed
                var filteredAchievements =
                    type == AchievementType.All
                        ? allAchievements
                        : allAchievements
                            .Where(a =>
                                string.Equals(
                                    a.Type.ToString(),
                                    type.ToString(),
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .ToList();

                // Get the first page of filtered achievements
                var unlockedAchievements = filteredAchievements.Skip(0).Take(5).ToList();

                // Create the embed
                string title = await _languageService.GetLocalizedStringAsync(
                    "AchievementsTitle",
                    lang
                );
                title = string.Format(title, e.User.Username);

                string description = await _languageService.GetLocalizedStringAsync(
                    "AchievementsDescription",
                    lang
                );
                description = string.Format(
                    description,
                    filteredAchievements.Count(c => c.IsCompleted),
                    filteredAchievements.Count
                );

                var embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Description = description,
                    Color = DiscordColor.Green,
                };
                embed.WithThumbnail(e.User.AvatarUrl);
                int totalPages = (int)Math.Ceiling(filteredAchievements.Count / 5.0);
                embed.WithFooter($"Page 1/{totalPages}");

                // Add achievement fields
                foreach (var achievement in unlockedAchievements)
                {
                    bool isUnlocked = achievement.IsCompleted;
                    string statusSymbol = isUnlocked ? "✅" : "❌";

                    string? localizedName =
                        achievement.Secret && !isUnlocked
                            ? await _languageService.GetLocalizedStringAsync(
                                "SecretAchievement",
                                lang
                            )
                            : await _languageService.GetLocalizedStringAsync(
                                achievement.Name,
                                lang
                            );

                    string? localizedDescription =
                        achievement.Secret && !isUnlocked
                            ? await _languageService.GetLocalizedStringAsync(
                                "SecretAchievementDescription",
                                lang
                            )
                            : await _languageService.GetLocalizedStringAsync(
                                achievement.Description!,
                                lang
                            );

                    embed.AddField(
                        $"{statusSymbol} {localizedName}",
                        "╰➤ " + localizedDescription!,
                        false
                    );
                }

                // Create achievement type selector
                string allOption =
                    await _languageService.GetLocalizedStringAsync("AchievementTypeAllOption", lang)
                    ?? "All Achievements";
                string milestoneOption =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementTypeMilestoneOption",
                        lang
                    ) ?? "Milestone Achievements";
                string skillOption =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementTypeSkillOption",
                        lang
                    ) ?? "Skill Achievements";
                string collectionOption =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementTypeCollectionOption",
                        lang
                    ) ?? "Collection Achievements";
                string timeBasedOption =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementTypeTimeBasedOption",
                        lang
                    ) ?? "Time-Based Achievements";
                string selectorLabel =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementTypeSelectorLabel",
                        lang
                    ) ?? "Filter by Type";

                var options = new List<DiscordSelectComponentOption>
                {
                    new DiscordSelectComponentOption(
                        allOption,
                        AchievementType.All.ToString(),
                        "Show all achievements",
                        isDefault: type == AchievementType.All
                    ),
                    new DiscordSelectComponentOption(
                        milestoneOption,
                        AchievementType.Milestone.ToString(),
                        "Achievements based on counting milestones",
                        isDefault: type == AchievementType.Milestone
                    ),
                    new DiscordSelectComponentOption(
                        skillOption,
                        AchievementType.Skill.ToString(),
                        "Achievements based on counting skills",
                        isDefault: type == AchievementType.Skill
                    ),
                    new DiscordSelectComponentOption(
                        collectionOption,
                        AchievementType.Collection.ToString(),
                        "Achievements based on collecting items",
                        isDefault: type == AchievementType.Collection
                    ),
                    new DiscordSelectComponentOption(
                        timeBasedOption,
                        AchievementType.TimeBased.ToString(),
                        "Achievements based on time and activity",
                        isDefault: type == AchievementType.TimeBased
                    ),
                };

                var selectMenu = new DiscordSelectComponent(
                    "achievement_type_selector",
                    selectorLabel,
                    options
                );

                // Create navigation buttons
                var prevButton = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"previous_achievements_page",
                    DiscordEmoji.FromUnicode("⬅️"),
                    totalPages <= 1
                );

                var nextButton = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"next_achievements_page",
                    DiscordEmoji.FromUnicode("➡️"),
                    totalPages <= 1
                );

                var translateButton = new DiscordButtonComponent(
                    DiscordButtonStyle.Secondary,
                    $"translate_AchievementsTitle_Original",
                    DiscordEmoji.FromUnicode("🌐"),
                    false
                );

                // Create the message builder with the updated embed and components
                var messageBuilder = new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(selectMenu)
                    .AddComponents(prevButton, nextButton, translateButton);

                // Respond to the interaction with the updated message
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.UpdateMessage,
                    messageBuilder
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling achievement type selection");
                string errorMessage =
                    await _languageService.GetLocalizedStringAsync(
                        "AchievementComponentErrorMessage",
                        "en"
                    ) ?? "An error occurred while processing your request.";
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(errorMessage)
                        .AsEphemeral(true)
                );
            }
        }
    }
}
