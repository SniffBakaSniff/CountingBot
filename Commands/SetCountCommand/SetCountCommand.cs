using System.ComponentModel;
using CountingBot.Features.Attributes;
using CountingBot.Helpers;
using CountingBot.Services;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("setcount")]
        [Description("Sets the current count for the channel.")]
        [PermissionCheck("setcount_command")]
        public async Task SetCountCommand(
            CommandContext ctx,
            [Description("The channel to change the count for.")]
            [SlashAutoCompleteProvider(typeof(CountingChannelsAutoComplete))]
                ulong channel,
            [Description("The new count value.")] int newCount
        )
        {
            Log.Information(
                "SetCountCommand invoked by {User} in guild {GuildId} for channel {ChannelId} with new count {NewCount}.",
                ctx.User.Username,
                ctx.Guild?.Id,
                channel,
                newCount
            );

            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            if (newCount < 0)
            {
                Log.Warning(
                    "Invalid number value {NewCount} provided by {User}.",
                    newCount,
                    ctx.User.Username
                );

                string invalidNumberTitle = await _languageService.GetLocalizedStringAsync(
                    "InvalidNumberTitle",
                    lang
                );
                string invalidNumberMsg = await _languageService.GetLocalizedStringAsync(
                    "InvalidNumberMessage",
                    lang
                );
                var errorEmbed = MessageHelpers.GenericErrorEmbed(
                    invalidNumberTitle,
                    invalidNumberMsg
                );
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(errorEmbed)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_InvalidNumberTitle_InvalidNumberMessage",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
                return;
            }

            try
            {
                if (!await _guildSettingsService.CheckIfCountingChannel(ctx.Guild!.Id, channel))
                {
                    Log.Warning(
                        "User {User} tried to set count in a non-counting channel {ChannelId}.",
                        ctx.User.Username,
                        channel
                    );

                    string notCountingChannelTitle = await _languageService.GetLocalizedStringAsync(
                        "InvalidChannel",
                        lang
                    );
                    string notCountingChannelMsg = await _languageService.GetLocalizedStringAsync(
                        "NotCountingChannel",
                        lang
                    );
                    var errorEmbed = MessageHelpers.GenericErrorEmbed(
                        notCountingChannelTitle,
                        notCountingChannelMsg
                    );
                    await ctx.RespondAsync(
                        new DiscordInteractionResponseBuilder()
                            .AddEmbed(errorEmbed)
                            .AsEphemeral(true)
                            .AddComponents(
                                new DiscordButtonComponent(
                                    DiscordButtonStyle.Secondary,
                                    $"translate_InvalidChannel_NotCountingChannel",
                                    DiscordEmoji.FromUnicode("🌐")
                                )
                            )
                    );
                    return;
                }

                await _guildSettingsService.SetChannelsCurrentCount(
                    ctx.Guild!.Id,
                    channel,
                    newCount
                );

                string countUpdatedTitle = await _languageService.GetLocalizedStringAsync(
                    "SetCountUpdatedTitle",
                    lang
                );
                string countUpdatedMsg = await _languageService.GetLocalizedStringAsync(
                    "SetCountUpdatedDescription",
                    lang
                );
                var successEmbed = MessageHelpers.GenericSuccessEmbed(
                    countUpdatedTitle,
                    string.Format(countUpdatedMsg, newCount)
                );

                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(successEmbed)
                        .AsEphemeral(false)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_SetCountUpdatedTitle_SetCountUpdatedDescription",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );

                Log.Information(
                    "Count successfully updated to {NewCount} for channel {ChannelId} by {User}.",
                    newCount,
                    channel,
                    ctx.User.Username
                );
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "An error occurred while updating the count for channel {ChannelId} in guild {GuildId}.",
                    channel,
                    ctx.Guild?.Id
                );

                string errorTitle = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorTitle",
                    lang
                );
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "GenericErrorMessage",
                    lang
                );
                var errorEmbed = MessageHelpers.GenericErrorEmbed(errorTitle, errorMsg);
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(errorEmbed)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_GenericErrorTitle_GenericErrorMessage",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
        }
    }
}
