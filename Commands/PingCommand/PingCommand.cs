using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("ping")]
        [Description("Checks the bot's response time and uptime.")]
        [PermissionCheck("ping_command", userBypass: true)]
        public async Task PingAsync(CommandContext ctx)
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            var latency = ctx.Client.GetConnectionLatency(ctx.Guild!.Id);
            var roundedLatency = Math.Round(latency.TotalMilliseconds);
            var uptime = DateTime.UtcNow - Program._botStartTime;

            var title = await _languageService.GetLocalizedStringAsync("PingTitle", lang);
            var latencyField = await _languageService.GetLocalizedStringAsync(
                "PingLatencyField",
                lang
            );
            var uptimeField = await _languageService.GetLocalizedStringAsync(
                "PingUptimeField",
                lang
            );

            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .AddField(latencyField, $"{roundedLatency} ms", true)
                .AddField(uptimeField, $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m", true)
                .WithColor(DiscordColor.Cyan)
                .WithTimestamp(DateTime.UtcNow);

            await ctx.RespondAsync(
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AsEphemeral(true)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_PingTitle_Original",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    )
            );
        }
    }
}
