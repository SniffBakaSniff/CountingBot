using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
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
        public async Task AchievementsCommandAsync(CommandContext ctx)
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            var unlockedAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                ctx.User.Id,
                pageSize: 5
            );
            var totalPages = await _userInformationService.GetUnlockedAchievementsAsync(
                ctx.User.Id,
                1,
                pageSize: 9999
            );

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
                totalPages.Count(c => c.IsCompleted),
                totalPages.Count
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
            embed.WithFooter($"Page 1/{(int)Math.Ceiling(totalPages.Count / 5.0)}");

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

            await ctx.RespondAsync(
                new DiscordWebhookBuilder()
                    .AddEmbed(embed)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"previous_achievements_page",
                            DiscordEmoji.FromUnicode("⬅️"),
                            false
                        ),
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"next_achievements_page",
                            DiscordEmoji.FromUnicode("➡️")
                        ),
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_AchievementsTitle_Original",
                            DiscordEmoji.FromUnicode("🌐"),
                            false
                        )
                    )
            );
        }
    }
}
