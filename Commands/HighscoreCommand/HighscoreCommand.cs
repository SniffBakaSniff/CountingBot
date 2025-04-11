using System.ComponentModel;
using CountingBot.Features.Attributes;
using CountingBot.Services;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("highscore")]
        [Description("Displays the highscore for the current channel.")]
        [PermissionCheck("highscore_command", userBypass: true)]
        public async Task HighscoreCommand(
            CommandContext ctx,
            [Description("The channel to get the highscore for.")]
            [SlashAutoCompleteProvider(typeof(CountingChannelsAutoComplete))]
                ulong? channel = null
        )
        {
            try
            {
                ulong guildId = ctx.Guild!.Id;

                // If channel is null, use the current channel
                ulong actualChannelId = channel ?? ctx.Channel.Id;

                // Check if the channel is a counting channel
                if (!await _guildSettingsService.CheckIfCountingChannel(guildId, actualChannelId))
                {
                    string errorLang =
                        await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                    string notCountingChannelMsg = await _languageService.GetLocalizedStringAsync(
                        "NotCountingChannel",
                        errorLang
                    );

                    await ctx.RespondAsync(
                        new DiscordInteractionResponseBuilder()
                            .WithContent(notCountingChannelMsg)
                            .AsEphemeral(true)
                            .AddComponents(
                                new DiscordButtonComponent(
                                    DiscordButtonStyle.Secondary,
                                    $"translate_{null}_NotCountingChannel",
                                    DiscordEmoji.FromUnicode("🌐")
                                )
                            )
                    );
                    return;
                }

                // Get the highscore and channel base
                string formattedHighscore = await _guildSettingsService.GetChannelHighscore(
                    guildId,
                    actualChannelId
                );
                int channelBase = await _guildSettingsService.GetChannelBase(
                    guildId,
                    actualChannelId
                );

                // Convert the highscore to decimal for display
                int decimalHighscore = 0;
                try
                {
                    decimalHighscore = Convert.ToInt32(formattedHighscore, channelBase);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "Error converting highscore {Highscore} from base {Base}",
                        formattedHighscore,
                        channelBase
                    );
                }

                // Get localized strings
                string lang =
                    await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                string title = await _languageService.GetLocalizedStringAsync(
                    "HighscoreTitle",
                    lang
                );
                string description = await _languageService.GetLocalizedStringAsync(
                    "HighscoreDescription",
                    lang
                );

                // Format the description with both the formatted highscore and decimal value
                string displayValue = formattedHighscore;
                if (channelBase != 10) // Only show decimal value if not already in decimal
                {
                    displayValue += $" ({decimalHighscore})";
                }
                description = string.Format(description, displayValue);

                // Create and send the embed
                var embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Description = description,
                    Color = DiscordColor.Gold,
                };

                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AsEphemeral(false)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_HighscoreTitle_Original",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );

                Log.Information(
                    "Highscore command executed for channel {ChannelId} with result {Highscore}",
                    actualChannelId,
                    displayValue
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing highscore command");
                string lang =
                    await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                    ?? "en";
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
