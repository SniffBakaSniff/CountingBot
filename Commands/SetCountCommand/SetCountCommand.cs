using System;
using System.Threading.Tasks;
using System.ComponentModel;
using CountingBot.Helpers;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Serilog;
using CountingBot.Services;

namespace CountingBot.Features.Commands
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

            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                          ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                          ?? "en";

            if (newCount < 0)
            {
                Log.Warning("Invalid number value {NewCount} provided by {User}.", newCount, ctx.User.Username);

                string invalidNumberTitle = await _languageService.GetLocalizedStringAsync("InvalidNumberTitle", lang);
                string invalidNumberMsg = await _languageService.GetLocalizedStringAsync("InvalidNumberMessage", lang);
                var errorEmbed = MessageHelpers.GenericErrorEmbed(invalidNumberTitle, invalidNumberMsg); 
                await ctx.RespondAsync(embed: errorEmbed);
                return;
            }

            try
            {
                if (!await _guildSettingsService.CheckIfCountingChannel(ctx.Guild!.Id, channel))
                {
                    Log.Warning("User {User} tried to set count in a non-counting channel {ChannelId}.", ctx.User.Username, channel);

                    string notCountingChannelTitle = await _languageService.GetLocalizedStringAsync("InvalidChannel", lang);
                    string notCountingChannelMsg = await _languageService.GetLocalizedStringAsync("NotCountingChannel", lang);
                    var errorEmbed = MessageHelpers.GenericErrorEmbed(notCountingChannelTitle, notCountingChannelMsg);
                    await ctx.RespondAsync(embed: errorEmbed);
                    return;
                }

                await _guildSettingsService.SetChannelsCurrentCount(ctx.Guild!.Id, channel, newCount);

                string countUpdatedMsg = await _languageService.GetLocalizedStringAsync("CountUpdated", lang);
                var successEmbed = MessageHelpers.GenericSuccessEmbed("Count Updated", string.Format(countUpdatedMsg, newCount));

                await ctx.RespondAsync(embed: successEmbed);

                Log.Information("Count successfully updated to {NewCount} for channel {ChannelId} by {User}.", newCount, channel, ctx.User.Username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while updating the count for channel {ChannelId} in guild {GuildId}.", channel, ctx.Guild?.Id);

                string errorMsg = await _languageService.GetLocalizedStringAsync("GenericErrorMessage", lang);
                var errorEmbed = MessageHelpers.GenericErrorEmbed("Error", errorMsg);
                await ctx.RespondAsync(embed: errorEmbed);
            }
        }
    }
}
