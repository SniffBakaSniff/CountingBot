using System.ComponentModel;
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
        [PermissionCheck("profile_command", userBypass:true)]
        public async Task ProfileCommand(CommandContext ctx)
        {
            ulong userId = ctx.User.Id;
            ulong guildId = ctx.Guild!.Id;

            string lang = await _userInformationService.GetUserPreferredLanguageAsync(userId)
                            ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                            ?? "en";
            try
            {
                Log.Information("Fetching compact profile for user {UserId}", userId);

                var userInfo = await _userInformationService.GetUserInformationAsync(userId);

                if (userInfo is null)
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

                string coinsLabel = await _languageService.GetLocalizedStringAsync("ProfileCoins", lang);
                string experienceLabel = await _languageService.GetLocalizedStringAsync("ProfileExperience", lang);
                string levelLabel = await _languageService.GetLocalizedStringAsync("ProfileLevel", lang);
                string revivesTemplate = await _languageService.GetLocalizedStringAsync("ProfileRevives", lang);
                string lastUpdatedLabel = await _languageService.GetLocalizedStringAsync("ProfileLastUpdated", lang);

                int xp = userInfo.ExperiencePoints;
                int maxXp = userInfo.Level * 100;

                embed.AddField(levelLabel, userInfo.Level.ToString(), false);
                embed.AddField(experienceLabel, $"{xp} / {maxXp}", false);
                embed.AddField(coinsLabel, userInfo.Coins.ToString(), false);
                embed.AddField(revivesTemplate, $"{userInfo.Revives} / 3");

                embed.WithFooter(lastUpdatedLabel).WithTimestamp(userInfo.LastUpdated);

                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral(false).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_ProfileTitle_Original", DiscordEmoji.FromUnicode("🌐"))
                ));
                Log.Information("Profile sent for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while fetching profile for user {UserId}", ctx.User.Id);
                string errorMsg = await _languageService.GetLocalizedStringAsync("GenericErrorMessage", lang);
                await ctx.RespondAsync(new DiscordInteractionResponseBuilder().WithContent(errorMsg).AsEphemeral(true).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_{null}_GenericErrorMessage", DiscordEmoji.FromUnicode("🌐"))
                ));
            }
        }

    }
}
