using System;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Serilog;
using System.Threading.Tasks;
using CountingBot.Services;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("profile")]
        [System.ComponentModel.Description("Displays the user's profile with counting statistics.")]
        public async Task ProfileAsync(CommandContext ctx)
        {
            try
            {
                ulong userId = ctx.User.Id;
                ulong guildId = ctx.Guild!.Id;

                Log.Information("Fetching profile for user {UserId}", userId);

                string lang = await _userInformationService.GetUserPreferredLanguageAsync(userId)
                              ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                              ?? "en";

                var userInfo = await _userInformationService.GetUserInformationAsync(userId);

                if (userInfo == null)
                {
                    string noProfileMsg = await _languageService.GetLocalizedStringAsync("NoProfileFound", lang);
                    await ctx.RespondAsync(noProfileMsg);
                    return;
                }

                string titleTemplate = await _languageService.GetLocalizedStringAsync("ProfileTitle", lang);
                string title = string.Format(titleTemplate, ctx.User.Username);

                var embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Color = DiscordColor.Blurple,
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ctx.User.AvatarUrl },
                    Timestamp = DateTime.UtcNow
                };

                string totalCountsLabel = await _languageService.GetLocalizedStringAsync("ProfileTotalCounts", lang);
                string correctCountsLabel = await _languageService.GetLocalizedStringAsync("ProfileCorrectCounts", lang);
                string incorrectCountsLabel = await _languageService.GetLocalizedStringAsync("ProfileIncorrectCounts", lang);
                string currentStreakLabel = await _languageService.GetLocalizedStringAsync("ProfileCurrentStreak", lang);
                string bestStreakLabel = await _languageService.GetLocalizedStringAsync("ProfileBestStreak", lang);
                string highestCountLabel = await _languageService.GetLocalizedStringAsync("ProfileHighestCount", lang);
                string coinsLabel = await _languageService.GetLocalizedStringAsync("ProfileCoins", lang);
                string experienceLabel = await _languageService.GetLocalizedStringAsync("ProfileExperience", lang);
                string levelLabel = await _languageService.GetLocalizedStringAsync("ProfileLevel", lang);
                string revivesTemplate = await _languageService.GetLocalizedStringAsync("ProfileRevives", lang);
                string challengesLabel = await _languageService.GetLocalizedStringAsync("ProfileChallenges", lang);
                string achievementsLabel = await _languageService.GetLocalizedStringAsync("ProfileAchievements", lang);
                string lastUpdatedLabel = await _languageService.GetLocalizedStringAsync("ProfileLastUpdated", lang);

                embed.AddField(totalCountsLabel, userInfo.CountingData[guildId].TotalCounts.ToString(), true);
                embed.AddField(correctCountsLabel, userInfo.CountingData[guildId].TotalCorrectCounts.ToString(), true);
                embed.AddField(incorrectCountsLabel, userInfo.CountingData[guildId].TotalIncorrectCounts.ToString(), true);
                embed.AddField(currentStreakLabel, userInfo.CountingData[guildId].CurrentStreak.ToString(), true);
                embed.AddField(bestStreakLabel, userInfo.CountingData[guildId].BestStreak.ToString(), true);
                embed.AddField(highestCountLabel, userInfo.CountingData[guildId].HighestCount.ToString(), true);
                embed.AddField(coinsLabel, userInfo.Coins.ToString(), true);
                embed.AddField(experienceLabel, userInfo.ExperiencePoints.ToString(), true);
                embed.AddField(levelLabel, userInfo.Level.ToString(), true);
                embed.AddField(revivesTemplate, $"{userInfo.Revives} (Used {userInfo.RevivesUsed})", true);
                embed.AddField(challengesLabel, userInfo.ChallengesCompleted.ToString(), true);
                embed.AddField(achievementsLabel, userInfo.AchievementsUnlocked.ToString(), true);
                embed.WithFooter(lastUpdatedLabel).WithTimestamp(userInfo.LastUpdated);
                await ctx.RespondAsync(embed: embed.Build());
                Log.Information("Profile sent successfully for user {UserId}", ctx.User.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while fetching the profile for user {UserId}", ctx.User.Id);
                string errorMsg = await _languageService.GetLocalizedStringAsync("GenericErrorMessage", "en");
                await ctx.RespondAsync(errorMsg);
            }
        }
    }
}
