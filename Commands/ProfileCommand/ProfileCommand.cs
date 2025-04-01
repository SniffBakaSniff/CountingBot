using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        [Command("profile")]
        [Description("Displays the user's profile with counting statistics.")]
        public async Task ProfileAsync(CommandContext ctx)
        {
            try
            {
                ulong userId = ctx.User.Id;
                ulong guildId = ctx.Guild!.Id;

                Log.Information("Fetching profile for user {UserId}", userId);

                var userInfo = await _userInforamtionService.GetUserInfoAsync(userId);

                if (userInfo == null)
                {
                    await ctx.RespondAsync("No profile found for you. Start counting to create your profile!");
                    return;
                }

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{ctx.User.Username}'s Profile",
                    Color = DiscordColor.Blurple,
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ctx.User.AvatarUrl },
                    Timestamp = DateTime.UtcNow
                };

                embed.AddField("Total Counts", userInfo.CountingData[guildId].TotalCount.ToString(), true);
                embed.AddField("Correct Counts", userInfo.CountingData[guildId].TotalCorrectCounts.ToString(), true);
                embed.AddField("Incorrect Counts", userInfo.CountingData[guildId].TotalIncorrectCounts.ToString(), true);
                embed.AddField("Current Streak", userInfo.CountingData[guildId].CurrentStreak.ToString(), true);
                embed.AddField("Best Streak", userInfo.CountingData[guildId].BestStreak.ToString(), true);
                embed.AddField("Highest Count", userInfo.CountingData[guildId].HighestCount.ToString(), true);
                embed.AddField("Coins", userInfo.Coins.ToString(), true);
                embed.AddField("Experience Points", userInfo.ExperiencePoints.ToString(), true);
                embed.AddField("Level", userInfo.Level.ToString(), true);
                embed.AddField("Revives", $"{userInfo.Revives} (Used: {userInfo.RevivesUsed})", true);
                embed.AddField("Challenges Completed", userInfo.ChallengesCompleted.ToString(), true);
                embed.AddField("Achievements Unlocked", userInfo.AchievementsUnlocked.ToString(), true);

                embed.WithFooter("Profile last updated").WithTimestamp(userInfo.LastUpdated);

                await ctx.RespondAsync(embed: embed);
                Log.Information("Profile sent successfully for user {UserId}", ctx.User.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while fetching the profile for user {UserId}", ctx.User.Id);
                await ctx.RespondAsync("An error occurred while fetching your profile. Please try again later.");
            }
        }
    }
}