using System.ComponentModel;
using CountingBot.Features.Attributes;
using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("setup")]
        [Description("Set up a counting channel")]
        [PermissionCheck("setup_command")]
        public async Task SetupCommandAsync(
            CommandContext ctx,
            DiscordChannel? channel,
            NumberSystem? type
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            if (type is null || channel is null)
            {
                string errorMessage = await _languageService.GetLocalizedStringAsync(
                    "SetupInvalidInput",
                    lang
                );
                var errorEmbed = MessageHelpers.GenericErrorEmbed(errorMessage);
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(errorEmbed)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_{null}_SetupInvalidInput",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
                return;
            }

            if (channel!.Type != DiscordChannelType.Text)
            {
                string errorMessage = await _languageService.GetLocalizedStringAsync(
                    "SetupInvalidChannel",
                    lang
                );
                var errorEmbed = MessageHelpers.GenericErrorEmbed(errorMessage);
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(errorEmbed)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_{null}_SetupInvalidChannel",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
                return;
            }

            int baseValue = (int)type.Value;

            Log.Information(
                "Setting up counting channel {ChannelId} in {GuildId} with base {Base}.",
                channel.Id,
                ctx.Guild!.Id,
                baseValue
            );

            await _guildSettingsService.SetCountingChannel(
                ctx.Guild.Id,
                channel.Id,
                baseValue,
                channel.Name
            );

            string successTitle = await _languageService.GetLocalizedStringAsync(
                "SetupSuccessTitle",
                lang
            );
            string successDescTemplate = await _languageService.GetLocalizedStringAsync(
                "SetupSuccessDescription",
                lang
            );
            string successDesc = string.Format(successDescTemplate, channel.Mention, baseValue);

            var successEmbed = MessageHelpers.GenericSuccessEmbed(successTitle, successDesc);
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
                "Counting channel {ChannelId} successfully set up in {GuildId} with base {Base}.",
                channel.Id,
                ctx.Guild!.Id,
                baseValue
            );
        }
    }

    public enum NumberSystem
    {
        [ChoiceDisplayName("Binary (Base-2)")]
        Binary = 2,

        [ChoiceDisplayName("Octal (Base-8)")]
        Octal = 8,

        [ChoiceDisplayName("Decimal (Base-10)")]
        Decimal = 10,

        [ChoiceDisplayName("Hexadecimal (Base-16)")]
        Hexadecimal = 16,
    }
}
