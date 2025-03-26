using System.Security.Cryptography.X509Certificates;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;


namespace CountingBot.Listeners
{
    public class OnMessageListener
    {
        private readonly BotDbContext dbContext = new BotDbContext();
        private int currentCount = 0;

        public OnMessageListener()
        {
        }

        //Very Basic Counting Logic (Placeholder)
        public async Task HandleMessage(DiscordClient client, MessageCreatedEventArgs ctx)
        {
            if (ctx.Author.IsBot)
            {
                return;
            }

            var correctEmoji = DiscordEmoji.FromName(client, ":white_check_mark:");
            var wrongEmoji = DiscordEmoji.FromName(client, ":x:");

            if (int.TryParse(ctx.Message.Content, out int parsedNumber))
            {
                if (parsedNumber <= currentCount || parsedNumber > currentCount + 1)
                {
                    await ctx.Message.CreateReactionAsync(wrongEmoji);
                    currentCount = 0;
                    await ctx.Message.RespondAsync("Count Ruined!");
                    return;
                }
                else if (parsedNumber == currentCount + 1)
                {
                    await ctx.Message.CreateReactionAsync(correctEmoji);
                    currentCount = parsedNumber;
                    return;
                }

            }
        }
    }
}
