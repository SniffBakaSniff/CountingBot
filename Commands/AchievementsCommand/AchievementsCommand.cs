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
        [PermissionCheck("achievements_command", userBypass:true)]
        public async Task AchievementsCommandAsync(CommandContext ctx)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                          ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                          ?? "en";
            
            var unlockedAchievements = await _userInformationService.GetUnlockedAchievementsAsync(ctx.User.Id, pageSize: 5);
            var totalPages = await _userInformationService.GetUnlockedAchievementsAsync(ctx.User.Id, 1, pageSize: 9999);

            string title = await _languageService.GetLocalizedStringAsync("AchievementsTitle", lang);
            title = string.Format(title, ctx.User.GlobalName);
            string description = await _languageService.GetLocalizedStringAsync("AchievementsDescription", lang);
            description = string.Format(description, totalPages.Count(c => c.IsCompleted), totalPages.Count);

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = DiscordColor.Green
            };
            embed.WithThumbnail(ctx.User.AvatarUrl);
            embed.WithFooter($"Page 1/{(int)Math.Ceiling(totalPages.Count / 5.0)}");

            foreach (var achievement in unlockedAchievements)
            {
                bool isUnlocked = achievement.IsCompleted;
                string statusSymbol = isUnlocked ? "✅" : "❌";
                
                string? localizedName = achievement.Secret && !isUnlocked
                    ? await _languageService.GetLocalizedStringAsync("SecretAchievement", lang)
                    : await _languageService.GetLocalizedStringAsync(achievement.Name, lang);

                string? localizedDescription = achievement.Secret && !isUnlocked 
                    ? await _languageService.GetLocalizedStringAsync("SecretAchievementDescription", lang)
                    : await _languageService.GetLocalizedStringAsync(achievement.Description!, lang);

                embed.AddField($"{statusSymbol} {localizedName}", "╰➤ " + localizedDescription!, false);
            }

            //List<DiscordSelectComponentOption> menuOptions = new()
            //{
            //    new DiscordSelectComponentOption("All", "All", "All achievements", isDefault: true),
            //    new DiscordSelectComponentOption("Milestone", "Milestone", "Progress-based achievements"),
            //    new DiscordSelectComponentOption("Skill", "Skill", "Achievements earned through performance"),
            //    new DiscordSelectComponentOption("Time Based", "TimeBased", "Achievements based on time or duration"),
            //    new DiscordSelectComponentOption("Collection", "Collection", "Achievements for gathering resources"),
            //    new DiscordSelectComponentOption("Challenge", "Challenge", "Achievements tied to specific challenges")
            //};

            //TODO: Finish this SelectComponent and the translate button
            await ctx.RespondAsync(new DiscordWebhookBuilder().AddEmbed(embed).AddComponents(
                //new DiscordSelectComponent("page_selector", "Catagorey", menuOptions)).AddComponents( // Might remove this component or might change it to filter for completed/uncompleted
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"previous_achievements_page", DiscordEmoji.FromUnicode("⬅️"), false),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"next_achievements_page", DiscordEmoji.FromUnicode("➡️")),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_AchievementsTitle_Original", DiscordEmoji.FromUnicode("🌐"), false))
            );
        }
    }
}