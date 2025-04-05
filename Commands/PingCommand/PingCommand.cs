using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("ping")]
        public async Task PingAsync(CommandContext ctx)
        {
            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                          ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                          ?? "en";

            var latency = ctx.Client.GetConnectionLatency(ctx.Guild!.Id);
            var roundedLatency = Math.Round(latency.TotalMilliseconds);
            var uptime = DateTime.UtcNow - Program._botStartTime;

            var title = await _languageService.GetLocalizedStringAsync("PingTitle", lang);
            var latencyField = await _languageService.GetLocalizedStringAsync("PingLatencyField", lang);
            var uptimeField = await _languageService.GetLocalizedStringAsync("PingUptimeField", lang);

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .AddField(latencyField, $"{roundedLatency} ms", true)
                .AddField(uptimeField, $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m", true)
                .WithColor(DiscordColor.Cyan)
                .WithTimestamp(DateTime.UtcNow);

            var response = new DiscordInteractionResponseBuilder()
                .AsEphemeral()
                .AddEmbed(embed.Build());

            await ctx.RespondAsync(response);
        }
    }
}