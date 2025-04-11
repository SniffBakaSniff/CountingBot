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
        [Command("Profile")]
        [Description("Displays the user's core profile stats.")]
        [PermissionCheck("profile_command", userBypass: true)]
        public async Task ProfileCommand(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;
            ulong guildId = ctx.Guild!.Id;

            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(userId)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";
            try
            {
                Log.Information("Fetching compact profile for user {UserId}", userId);

                var userInfo = await _userInformationService.GetUserInformationAsync(userId);

                // Get localized strings
                string titleTemplate = await _languageService.GetLocalizedStringAsync(
                    "ProfileTitle",
                    lang
                );
                string title = string.Format(
                    titleTemplate,
                    ctx.Guild!.GetMemberAsync(ctx.User.Id).Result.DisplayName
                );
                string coinsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileCoins",
                    lang
                );
                string revivesTemplate = await _languageService.GetLocalizedStringAsync(
                    "ProfileRevives",
                    lang
                );
                string lastUpdatedLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileLastUpdated",
                    lang
                );
                string profileStatsLabel = await _languageService.GetLocalizedStringAsync(
                    "ProfileStatsLabel",
                    lang
                );

                // Calculate and format values
                string formattedCoins = userInfo!.Coins.ToString("#,##0");

                // Create the embed
                var embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Color = DiscordColor.Blurple,
                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = ctx.User.AvatarUrl },
                    Timestamp = DateTime.UtcNow,
                };

                // Add profile stats
                embed.AddField($"📊 {profileStatsLabel}", "_ _", false);
                embed.AddField(
                    $"🌟 Level",
                    "╰➤ "
                        + userInfo.Level.ToString()
                        + $" ({userInfo.ExperiencePoints}/{userInfo.Level * 100} XP)",
                    true
                );
                embed.AddField($"💰 {coinsLabel}", "╰➤ " + formattedCoins, true);
                embed.AddField($"⚡ {revivesTemplate}", "╰➤ " + $"{userInfo.Revives}/3", true);

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
                Log.Information("Profile sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while fetching profile for user {UserId}", ctx.User.Id);
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
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
