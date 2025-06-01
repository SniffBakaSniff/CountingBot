using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        /// <summary>
        /// Command for viewing a user's achievements.
        /// </summary>
        [Command("achievements")]
        [Description("View your unlocked and locked achievements.")]
        [PermissionCheck("achievements_command", userBypass: true)]
        public async Task AchievementsCommandAsync(
            CommandContext ctx,
            AchievementType type = AchievementType.All
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            // Get all achievements
            var allAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                ctx.User.Id,
                1,
                pageSize: 9999
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

            string title = await _languageService.GetLocalizedStringAsync(
                "AchievementsTitle",
                lang
            );
            title = string.Format(title, ctx.Guild!.GetMemberAsync(ctx.User.Id).Result.DisplayName);
            string description = await _languageService.GetLocalizedStringAsync(
                "AchievementsDescription",
                lang
            );
            description = string.Format(
                description,
                filteredAchievements.Count(c => c.IsCompleted),
                filteredAchievements.Count
            );

            if (unlockedAchievements.Count == 0)
            {
                string noProfileMsg = await _languageService.GetLocalizedStringAsync(
                    "NoProfileFound",
                    lang
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .WithContent(noProfileMsg)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_{null}_NoProfileFound",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = DiscordColor.Green,
            };
            embed.WithThumbnail(ctx.User.AvatarUrl);
            int totalPages = (int)Math.Ceiling(filteredAchievements.Count / 5.0);
            embed.WithFooter($"Page 1/{totalPages}");

            foreach (var achievement in unlockedAchievements)
            {
                bool isUnlocked = achievement.IsCompleted;
                string statusSymbol = isUnlocked ? "✅" : "❌";

                string? localizedName =
                    achievement.Secret && !isUnlocked
                        ? await _languageService.GetLocalizedStringAsync("SecretAchievement", lang)
                        : await _languageService.GetLocalizedStringAsync(achievement.Name, lang);

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
                await _languageService.GetLocalizedStringAsync("AchievementTypeSkillOption", lang)
                ?? "Skill Achievements";
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
                await _languageService.GetLocalizedStringAsync("AchievementTypeSelectorLabel", lang)
                ?? "Filter by Type";

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

            await ctx.RespondAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(embed)
                    .AddComponents(selectMenu)
                    .AddComponents(prevButton, nextButton, translateButton)
            );
        }
    }
}
