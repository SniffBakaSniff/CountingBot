using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using CountingBot.Helpers;

using CountingBot.Database;

namespace CountingBot.Listeners
{
    public class ButtonInteractionListener
    {
        private readonly BotDbContext dbContext = new BotDbContext();

        public ButtonInteractionListener()
        {
        }

        public async Task HandleButtonInteraction(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            switch (e.Id)
            {
                case "button":

                    var embed = MessageHelpers.GenericEmbed("Button","You Pressed A Button!!");

                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                            new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral(true));
                    return;
                
                default:
                    throw new InvalidOperationException($"Unhandled button interaction: {e.Id}");
            }
        }
    }
}
