using System.ComponentModel;
using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.ConfigCommands
{
    public partial class CommandsGroup
    {
        [Command("setcount")]
        [Description("Sets the current count for the channel.")]
        public async Task SetCountCommand(
            CommandContext ctx,
            [Description("The channel to change the count for.")]
            [SlashAutoCompleteProvider(typeof(CountingChannelsAutoComplete))] ulong channel,
            [Description("The new count value.")] int newCount)
        {
            Log.Information("SetCountCommand invoked by {User} in guild {GuildId} for channel {ChannelId} with new count {NewCount}.",
                ctx.User.Username, ctx.Guild?.Id, channel, newCount);

            if (newCount < 0)
            {
                Log.Warning("Invalid number value {NewCount} provided by {User}.", newCount, ctx.User.Username);

                var errorEmbed = new DiscordEmbedBuilder
                {
                    Title = "Invalid number",
                    Description = "The number cannot be negative.",
                    Color = DiscordColor.Red
                };
                await ctx.RespondAsync(embed: errorEmbed);
                return;
            }

            try
            {
                await _guildSettingsService.SetChannelsCurrentCount(ctx.Guild!.Id, channel, newCount);

                var successEmbed = MessageHelpers.GenericSuccessEmbed(
                    "Count Updated",
                    $"The count for the selected channel has been set to **{newCount}**."
                );

                await ctx.RespondAsync(embed: successEmbed);

                Log.Information("Count successfully updated to {NewCount} for channel {ChannelId} by {User}.", newCount, channel, ctx.User.Username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating the count for channel {ChannelId} in guild {GuildId}.", channel, ctx.Guild?.Id);

                var errorEmbed = new DiscordEmbedBuilder
                {
                    Title = "Error",
                    Description = "An error occurred while updating the count. Please try again later.",
                    Color = DiscordColor.Red
                };
                await ctx.RespondAsync(embed: errorEmbed);
            }
        }
    }
}