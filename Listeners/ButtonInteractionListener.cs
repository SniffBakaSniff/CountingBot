using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;
using CountingBot.Helpers;
using CountingBot.Services.Database;

namespace CountingBot.Listeners
{
    public class ButtonInteractionListener
    {
        private readonly IUserInformationService _userInformationService;
        private readonly IGuildSettingsService _guildSettingsService;

        public ButtonInteractionListener(
            IGuildSettingsService guildSettingsService,
            IUserInformationService userInformationService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
        }

        public async Task HandleButtonInteraction(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            try
            {
                Log.Debug("Handling button interaction from {UserId}", e.User.Id);

                var referencedMessage = await GetReferencedMessageAsync(e).ConfigureAwait(false);
                if (referencedMessage == null) return;

                switch (e.Id)
                {
                    case "use_revive":
                        await HandleReviveUsageAsync(e, referencedMessage).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling button interaction");
            }
        }

        private async Task HandleReviveUsageAsync(ComponentInteractionCreatedEventArgs e, DiscordMessage referencedMessage)
        {
            var revivesAvailable = await _userInformationService.GetUserRevivesAsync(e.User.Id, false).ConfigureAwait(false);
            if (!revivesAvailable)
            {
                await RespondNoRevivesAsync(e).ConfigureAwait(false);
                return;
            }

            await _userInformationService.GetUserRevivesAsync(e.User.Id, true).ConfigureAwait(false);

            var (baseValue, currentCount) = await GetCountDetailsAsync(e.Guild.Id, e.Channel.Id).ConfigureAwait(false);
            var parsedCount = Convert.ToString(currentCount, baseValue);

            await HandleSuccessfulReviveAsync(e, referencedMessage, parsedCount).ConfigureAwait(false);
        }

        private async Task HandleSuccessfulReviveAsync(ComponentInteractionCreatedEventArgs e, DiscordMessage referencedMessage, string parsedCount)
        {
            try
            {
                await referencedMessage.DeleteAsync().ConfigureAwait(false);
                await e.Message.DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete referenced message {MessageId}", referencedMessage.Id);
            }

            var (baseValue, currentCount) = await GetCountDetailsAsync(e.Guild.Id, e.Channel.Id).ConfigureAwait(false);
            var nextCount = Convert.ToString(currentCount + 1, baseValue);

            var revivedEmbed = new DiscordEmbedBuilder()
                .WithTitle("✨ Count Revived!")
                .WithDescription($"The count is back! It's time to continue the challenge, the next number to count is **{nextCount}**.")
                .WithColor(DiscordColor.Green)
                .WithTimestamp(DateTime.UtcNow)
                .Build();

            await e.Channel.SendMessageAsync(embed: revivedEmbed).ConfigureAwait(false);
        }


        private static async Task<DiscordMessage?> GetReferencedMessageAsync(ComponentInteractionCreatedEventArgs e)
        {
            if (e.Message.Reference?.Message == null)
            {
                Log.Warning("No referenced message for interaction {InteractionId}", e.Id);
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Error: Original message not found"));
                return null;
            }

            return await e.Channel.GetMessageAsync(e.Message.Reference.Message.Id).ConfigureAwait(false);
        }

        private static async Task RespondNoRevivesAsync(ComponentInteractionCreatedEventArgs e)
        {
            Log.Information("User {UserId} has no revives", e.User.Id);
            await RespondToInteractionAsync(e, "You have no revives left!").ConfigureAwait(false);
        }

        private static async Task RespondToInteractionAsync(ComponentInteractionCreatedEventArgs e, string content)
        {
            await e.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder().WithContent(content)
            ).ConfigureAwait(false);
        }

        private async Task<(int baseValue, int currentCount)> GetCountDetailsAsync(ulong guildId, ulong channelId)
        {
            return (
                await _guildSettingsService.GetChannelBase(guildId, channelId).ConfigureAwait(false),
                await _guildSettingsService.GetChannelsCurrentCount(guildId, channelId).ConfigureAwait(false)
            );
        }
    }
}