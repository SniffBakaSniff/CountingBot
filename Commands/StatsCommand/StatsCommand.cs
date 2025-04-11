using System.ComponentModel;
using System.Text;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("stats")]
        [Description("Displays the user's profile with counting statistics.")]
        [PermissionCheck("stats_command", userBypass: true)]
        public async Task StatsCommand(CommandContext ctx)
        {
            try
            {
                ulong userId = ctx.User.Id;
                ulong guildId = ctx.Guild!.Id;

                Log.Information("Fetching profile for user {UserId}", userId);

                string lang =
                    await _userInformationService.GetUserPreferredLanguageAsync(userId)
                    ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                    ?? "en";

                var userInfo = await _userInformationService.GetUserInformationAsync(userId);

                if (userInfo is null)
                {
                    string noProfileMsg = await _languageService.GetLocalizedStringAsync(
                        "NoProfileFound",
                        lang
                    );
                    await ctx.RespondAsync(noProfileMsg);
                    return;
                }

                string titleTemplate = await _languageService.GetLocalizedStringAsync(
                    "StatsProfileTitle",
                    lang
                );
                string title = string.Format(
                    titleTemplate,
                    ctx.Guild!.GetMemberAsync(ctx.User.Id).Result.DisplayName
                );

                // Get localized strings for all labels
                string totalCountsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileTotalCounts",
                    lang
                );
                string correctCountsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileCorrectCounts",
                    lang
                );
                string incorrectCountsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileIncorrectCounts",
                    lang
                );
                string currentStreakLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileCurrentStreak",
                    lang
                );
                string bestStreakLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileBestStreak",
                    lang
                );
                string highestCountLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileHighestCount",
                    lang
                );
                string coinsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileCoins",
                    lang
                );

                string revivesTemplate = await _languageService.GetLocalizedStringAsync(
                    "ProfileRevives",
                    lang
                );
                string achievementsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileAchievements",
                    lang
                );
                string lastUpdatedLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileLastUpdated",
                    lang
                );
                string statsHeaderLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileStatsHeader",
                    lang
                );

                try
                {
                    // Get counting data for this guild
                    var countingData = userInfo.CountingData[guildId];

                    // Format numbers with commas for better readability
                    string formattedTotalCounts = countingData.TotalCounts.ToString("#,##0");
                    string formattedCorrectCounts = countingData.TotalCorrectCounts.ToString(
                        "#,##0"
                    );
                    string formattedIncorrectCounts = countingData.TotalIncorrectCounts.ToString(
                        "#,##0"
                    );
                    string formattedHighestCount = countingData.HighestCount.ToString("#,##0");
                    string formattedCoins = userInfo.Coins.ToString("#,##0");

                    // Create the embed
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = title,
                        Color = DiscordColor.Blurple,
                        Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                        {
                            Url = ctx.User.AvatarUrl,
                        },
                        Timestamp = DateTime.UtcNow,
                    };

                    // Add counting statistics section
                    embed.AddField($"📊 {statsHeaderLabel}", "_ _", false);
                    embed.AddField($"🔢 {totalCountsLabel}", "╰➤ " + formattedTotalCounts, true);
                    embed.AddField($"✅ {correctCountsLabel}", "╰➤ " + formattedCorrectCounts, true);
                    embed.AddField(
                        $"❌ {incorrectCountsLabel}",
                        "╰➤ " + formattedIncorrectCounts,
                        true
                    );
                    embed.AddField($"📈 {highestCountLabel}", "╰➤ " + formattedHighestCount, true);
                    embed.AddField(
                        $"🔥 {currentStreakLabel}",
                        "╰➤ " + countingData.CurrentStreak.ToString(),
                        true
                    );
                    embed.AddField(
                        $"🏆 {bestStreakLabel}",
                        "╰➤ " + countingData.BestStreak.ToString(),
                        true
                    );
                    embed.AddField($"💰 {coinsLabel}", "╰➤ " + formattedCoins, true);
                    embed.AddField(
                        $"⚡ {revivesTemplate}",
                        "╰➤ " + $"{userInfo.Revives}/3 ({userInfo.RevivesUsed} used)",
                        true
                    );
                    embed.AddField(
                        $"🏅 {achievementsLabel}",
                        "╰➤ " + userInfo.UnlockedAchievements.Count.ToString(),
                        true
                    );

                    // Add footer with last updated timestamp
                    embed.WithFooter(lastUpdatedLabel).WithTimestamp(userInfo.LastUpdated);
                    await ctx.RespondAsync(
                        new DiscordInteractionResponseBuilder()
                            .AddEmbed(embed)
                            .AsEphemeral(false)
                            .AddComponents(
                                new DiscordButtonComponent(
                                    DiscordButtonStyle.Secondary,
                                    $"translate_ProfileTitle_Original",
                                    DiscordEmoji.FromUnicode("🌐")
                                )
                            )
                    );
                    Log.Information("Profile sent successfully for user {UserId}", ctx.User.Id);
                }
                catch (KeyNotFoundException ex)
                {
                    Log.Debug(
                        ex,
                        "Key {GuildId} was not foung in UserInformation.CountingData for user {UserId}",
                        guildId,
                        userId
                    );
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
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "An error occurred while fetching the profile for user {UserId}",
                    ctx.User.Id
                );
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    "en"
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .WithContent(errorMsg)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_{null}_GenericErrorMessage",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
        }
    }
}
