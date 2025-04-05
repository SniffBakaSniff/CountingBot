using System.Text;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Serilog;

using CountingBot.Database.Models;

// TODO: Implement Top Guilds Leaderboard
namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("leaderboard")]
        [Description("Displays the leaderboard for your guild or globally.")]
        public async Task LeaderboardCommand(CommandContext ctx, Type type = Type.Guild, LeaderboardCategory leaderboardCategory = LeaderboardCategory.TotalCounts, int page = 1)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                        ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                        ?? "en";

            Log.Information("Leaderboard command called by {UserName} (ID: {UserId}) for {GuildName} (ID: {GuildId}). Type: {Type}, Category: {LeaderboardCategory}, Page: {Page}",
                ctx.User.Username, ctx.User.Id, ctx.Guild!.Name, ctx.Guild.Id, type, leaderboardCategory, page);

            switch (type)
            {
                case Type.Guild:
                    Log.Information("Fetching guild leaderboard data for guild ID: {GuildId}", ctx.Guild.Id);
                    await HandleLeaderboardAsync(ctx, lang, page, ctx.Guild!.Id, leaderboardCategory, false);
                    break;

                case Type.Global:
                    Log.Information("Fetching global leaderboard data.");
                    await HandleLeaderboardAsync(ctx, lang, page, ctx.Guild!.Id, leaderboardCategory, true);
                    break;
            }
        }

        private async Task HandleLeaderboardAsync(CommandContext ctx, string lang, int page, ulong guildId, LeaderboardCategory leaderboardCategory, bool isGlobal)
        {
            Log.Information("Fetching leaderboard data (Type: {Type}, Category: {LeaderboardCategory}, Page: {Page})", isGlobal ? "Global" : "Guild", leaderboardCategory, page);

            var leaderboards = await _leaderboardService.GetLeaderboardsAsync(page, 10, guildId);

            string leaderboardKey = isGlobal ? $"Global{leaderboardCategory}" : leaderboardCategory.ToString();

            if (!leaderboards.ContainsKey(leaderboardKey))
            {
                Log.Warning("Leaderboard data for {LeaderboardCategory} not found for guild ID: {GuildId}", leaderboardCategory, guildId);
                await ctx.RespondAsync("Invalid leaderboard type.");
                return;
            }

            var leaderboardData = leaderboards[leaderboardKey];

            if (leaderboardData.Count == 0)
            {
                Log.Information("No leaderboard data available for {LeaderboardCategory} (Guild ID: {GuildId})", leaderboardCategory, guildId);
                await ctx.RespondAsync("No data available for this leaderboard.");
                return;
            }

            string title = await GetLeaderboardTitleAsync(leaderboardCategory, lang, isGlobal);
            var leaderboardText = await BuildLeaderboardText(ctx, leaderboardData, guildId, leaderboardCategory);

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(leaderboardText)
                .WithColor(DiscordColor.Gold);

            Log.Information("Sending leaderboard response for {LeaderboardCategory} (Guild ID: {GuildId})", leaderboardCategory, guildId);
            await ctx.RespondAsync(embed);
        }

        private static async Task<string> BuildLeaderboardText(CommandContext ctx, List<UserInformation> leaderboardData, ulong guildId, LeaderboardCategory leaderboardCategory)
        {
            var leaderboardText = new StringBuilder();
            int rank = 1;

            foreach (var userInfo in leaderboardData)
            {
                string userName = await GetUserDisplayName(ctx, userInfo.UserId);

                var stats = userInfo.CountingData.FirstOrDefault(c => c.Key == guildId).Value;

                int displayValue = leaderboardCategory switch
                {
                    LeaderboardCategory.TotalCounts => stats.TotalCounts,
                    LeaderboardCategory.HighestCount => stats.HighestCount,
                    LeaderboardCategory.TotalCorrectCounts => stats.TotalCorrectCounts,
                    LeaderboardCategory.CurrentStreak => stats.CurrentStreak,
                    LeaderboardCategory.BestStreak => stats.BestStreak,
                    _ => stats.TotalCounts
                };

                leaderboardText.AppendLine($"**#{rank}** ---- {userName} ---- **{displayValue}**");
                leaderboardText.AppendLine("-# --------------------------------------------");
                rank++;
            }

            return leaderboardText.ToString();
        }

        private async Task<string> GetLeaderboardTitleAsync(LeaderboardCategory type, string lang, bool isGlobal)
        {
            string key = type switch
            {
                LeaderboardCategory.HighestCount => "LeaderboardCategoryHighestCount",
                LeaderboardCategory.TotalCounts => "LeaderboardCategoryTotalCounts",
                LeaderboardCategory.TotalCorrectCounts => "LeaderboardCategoryTotalCorrectCounts",
                LeaderboardCategory.BestStreak => "LeaderboardCategoryBestStreak",
                LeaderboardCategory.CurrentStreak => "LeaderboardCategoryCurrentStreak",
                _ => "DefaultLeaderboardTitle"
            };

            string leaderboardType = isGlobal ? "Global" : "Guild";

            Log.Information("Fetching title for {LeaderboardType} leaderboard: {Key}", leaderboardType, key);

            return await _languageService.GetLocalizedStringAsync($"{leaderboardType}{key}", lang);
        }

        private static async Task<string> GetUserDisplayName(CommandContext ctx, ulong userId)
        {
            Log.Information("Fetching display name for user ID: {UserId} in guild ID: {GuildId}", userId, ctx.Guild!.Id);

            var member = await ctx.Client.GetUserAsync(userId);

            if (member is not null)
            {
                string displayName = member.GlobalName.Length > 19 ? member.GlobalName[..19] + "..." : member.GlobalName;
                Log.Information("User found. Display name: {DisplayName}", displayName);
                return displayName;
            }

            Log.Warning("User with ID: {UserId} not found in guild ID: {GuildId}", userId, ctx.Guild!.Id);
            return "Unknown User";
        }
    }

    public enum Type
    {
        [ChoiceDisplayName("Guild")]
        Guild,

        [ChoiceDisplayName("Global")]
        Global
    }

    public enum LeaderboardCategory
    {
        [Description("Total Counts")]
        TotalCounts,

        [Description("Highest Count")]
        HighestCount,

        [Description("Total Correct Counts")]
        TotalCorrectCounts,

        [Description("Current Streak")]
        CurrentStreak,

        [Description("Best Streak")]
        BestStreak
    }
}
